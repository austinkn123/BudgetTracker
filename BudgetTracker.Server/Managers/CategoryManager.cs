using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

public class CategoryManager(
    ICategoryEngine engine,
    ICategoryAccessor accessor,
    ITransactionAccessor transactionAccessor,
    IPlaidEngine plaidEngine,
    IDataVersionAccessor dataVersion) : ICategoryManager
{
    public async Task<Result<Category>> GetByIdAsync(int id, int userId)
    {
        var category = await accessor.GetByIdForUserAsync(id, userId);
        return category is not null
            ? Result<Category>.Success(category)
            : Result<Category>.Failure("Category not found");
    }

    public async Task<Result<IEnumerable<Category>>> GetByUserIdAsync(int userId)
    {
        var categories = await accessor.GetByUserIdAsync(userId);
        return Result<IEnumerable<Category>>.Success(categories);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CategoryUsage>>> GetUsageAsync(int userId, DateTime since)
    {
        var usage = await transactionAccessor.GetCategoryUsageAsync(userId, since);
        return Result<IReadOnlyList<CategoryUsage>>.Success(usage);
    }

    public async Task<Result<int>> CreateAsync(Category category)
    {
        var error = engine.ValidateCategory(category);
        if (error is not null)
            return Result<int>.Failure(error);

        var otherNames = await accessor.GetOtherNamesAsync(category.UserId, excludeCategoryId: 0);
        var duplicateError = engine.ValidateNameIsUnique(category.Name, otherNames);
        if (duplicateError is not null)
            return Result<int>.Failure(duplicateError);

        category.PlaidCategoryPrimary = string.IsNullOrWhiteSpace(category.PlaidCategoryPrimary)
            ? null
            : category.PlaidCategoryPrimary.Trim();

        int id;
        try
        {
            id = await accessor.CreateAsync(category);
        }
        catch (UniqueNameViolationException)
        {
            return Result<int>.Failure(engine.DuplicateNameError(category.Name));
        }

        await dataVersion.BumpAsync(category.UserId);
        return Result<int>.Success(id);
    }

    public async Task<Result<bool>> UpdateAsync(Category category)
    {
        var error = engine.ValidateCategory(category);
        if (error is not null)
            return Result<bool>.Failure(error);

        var existing = await accessor.GetByIdForUserAsync(category.Id, category.UserId);
        if (existing is null)
            return Result<bool>.Failure("Category not found");

        var otherNames = await accessor.GetOtherNamesAsync(category.UserId, category.Id);
        var duplicateError = engine.ValidateNameIsUnique(category.Name, otherNames);
        if (duplicateError is not null)
            return Result<bool>.Failure(duplicateError);

        var typeChanged = existing.CategoryType != category.CategoryType;
        existing.Name = category.Name;
        existing.CategoryType = category.CategoryType;

        // null means "not supplied", NOT "clear it". The category form PUTs only name and type, so
        // nulling this on every update would silently destroy the Plaid auto-categorisation mapping
        // on a simple rename. Clearing is done by sending an explicit empty string.
        if (category.PlaidCategoryPrimary is not null)
        {
            existing.PlaidCategoryPrimary = string.IsNullOrWhiteSpace(category.PlaidCategoryPrimary)
                ? null
                : category.PlaidCategoryPrimary.Trim();
        }

        bool updated;
        try
        {
            updated = await accessor.UpdateAsync(existing);
        }
        catch (UniqueNameViolationException)
        {
            return Result<bool>.Failure(engine.DuplicateNameError(category.Name));
        }

        if (!updated)
            return Result<bool>.Failure("Category not found");

        // The category type is the user's Income/Expense override, so its rows follow it (Transfer and Adjustment
        // rows keep their type). ponytail: a second save, not atomic with the category's; a unit of work is the
        // upgrade path if a half-applied change is ever seen.
        try
        {
            if (typeChanged)
            {
                var rows = await transactionAccessor.GetFilteredAsync(existing.UserId, new TransactionFilter(CategoryId: existing.Id));
                var types = rows.ToDictionary(
                    r => r.Id,
                    r => plaidEngine.ClassifyTransactionType(r.PlaidCategoryPrimary, existing.CategoryType, r.TransactionType));
                await transactionAccessor.SetCategoryAsync(types, existing.Id, existing.UserId);
            }
        }
        finally
        {
            // The category row is already saved, so clients must refetch even if the retype throws (it still propagates).
            await dataVersion.BumpAsync(existing.UserId);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> DeleteAsync(int id, int userId)
    {
        bool deleted;
        try
        {
            deleted = await accessor.DeleteAsync(id, userId);
        }
        catch (EntityInUseException)
        {
            return Result<bool>.Failure(engine.InUseError());
        }

        if (!deleted)
            return Result<bool>.Failure("Category not found");

        await dataVersion.BumpAsync(userId);
        return Result<bool>.Success(true);
    }
}
