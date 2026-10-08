using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Server.Managers;

public class CategoryManager(ICategoryEngine engine, ICategoryAccessor accessor) : ICategoryManager
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

        try
        {
            return Result<int>.Success(await accessor.CreateAsync(category));
        }
        catch (UniqueNameViolationException)
        {
            return Result<int>.Failure(engine.DuplicateNameError(category.Name));
        }
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

        return updated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Category not found");
    }

    // TODO: CategoryManager violates IDesign layering (catches an EF exception type) — consult tony.
    public async Task<Result<bool>> DeleteAsync(int id, int userId)
    {
        bool deleted;
        try
        {
            deleted = await accessor.DeleteAsync(id, userId);
        }
        catch (DbUpdateException)
        {
            return Result<bool>.Failure("Category is in use and cannot be deleted");
        }

        return deleted
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Category not found");
    }
}
