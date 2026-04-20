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

        var id = await accessor.CreateAsync(category);
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

        existing.Name = category.Name;
        existing.CategoryType = category.CategoryType;

        var updated = await accessor.UpdateAsync(existing);
        return updated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Category not found");
    }

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
