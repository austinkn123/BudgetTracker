using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

public class CategoryManager(ICategoryEngine engine, ICategoryAccessor accessor) : ICategoryManager
{
    public async Task<Result<Category>> GetByIdAsync(int id)
    {
        var category = await accessor.GetByIdAsync(id);
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

        var updated = await accessor.UpdateAsync(category);
        return updated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Category not found");
    }

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var deleted = await accessor.DeleteAsync(id);
        return deleted
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Category not found");
    }
}
