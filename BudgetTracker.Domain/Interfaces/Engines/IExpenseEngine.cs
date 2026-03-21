using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface IExpenseEngine
{
    string? ValidateExpense(Expense expense);
}
