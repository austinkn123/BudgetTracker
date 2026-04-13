using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface IBudgetPlanEngine
{
    string? ValidateBudgetPlan(BudgetPlan budgetPlan);
    void NormalizeForPersistence(BudgetPlan budgetPlan);
}