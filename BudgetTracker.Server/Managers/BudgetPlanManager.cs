using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

public class BudgetPlanManager(IBudgetPlanEngine engine, IBudgetPlanAccessor accessor) : IBudgetPlanManager
{
    public async Task<Result<BudgetPlan>> GetByIdAsync(int id, int userId)
    {
        var budgetPlan = await accessor.GetByIdAsync(id, userId);
        return budgetPlan is not null
            ? Result<BudgetPlan>.Success(budgetPlan)
            : Result<BudgetPlan>.Failure("Budget plan not found");
    }

    public async Task<Result<IEnumerable<BudgetPlan>>> GetByUserIdAsync(int userId)
    {
        var budgetPlans = await accessor.GetByUserIdAsync(userId);
        return Result<IEnumerable<BudgetPlan>>.Success(budgetPlans);
    }

    public async Task<Result<int>> CreateAsync(BudgetPlan budgetPlan, int userId)
    {
        budgetPlan.UserId = userId;
        engine.NormalizeForPersistence(budgetPlan);

        var error = engine.ValidateBudgetPlan(budgetPlan);
        if (error is not null)
            return Result<int>.Failure(error);

        var categoryOwnershipError = await ValidateCategoryOwnershipAsync(budgetPlan, userId);
        if (categoryOwnershipError is not null)
            return Result<int>.Failure(categoryOwnershipError);

        var id = await accessor.CreateAsync(budgetPlan);
        return Result<int>.Success(id);
    }

    public async Task<Result<bool>> UpdateAsync(BudgetPlan budgetPlan, int userId)
    {
        budgetPlan.UserId = userId;
        engine.NormalizeForPersistence(budgetPlan);

        var error = engine.ValidateBudgetPlan(budgetPlan);
        if (error is not null)
            return Result<bool>.Failure(error);

        var categoryOwnershipError = await ValidateCategoryOwnershipAsync(budgetPlan, userId);
        if (categoryOwnershipError is not null)
            return Result<bool>.Failure(categoryOwnershipError);

        var updated = await accessor.UpdateAsync(budgetPlan, userId);
        return updated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Budget plan not found");
    }

    public async Task<Result<bool>> DeleteAsync(int id, int userId)
    {
        var deleted = await accessor.DeleteAsync(id, userId);
        return deleted
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Budget plan not found");
    }

    private async Task<string?> ValidateCategoryOwnershipAsync(BudgetPlan budgetPlan, int userId)
    {
        var categoryIds = budgetPlan.Lines
            .Where(l => l.CategoryId.HasValue)
            .Select(l => l.CategoryId!.Value)
            .Distinct()
            .ToArray();

        if (categoryIds.Length == 0)
            return null;

        var categoriesBelongToUser = await accessor.CategoriesBelongToUserAsync(categoryIds, userId);
        return categoriesBelongToUser
            ? null
            : "One or more categories do not belong to current user";
    }
}