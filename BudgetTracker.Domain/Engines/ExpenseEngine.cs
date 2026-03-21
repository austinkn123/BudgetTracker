using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Engines;

public class ExpenseEngine : IExpenseEngine
{
    public string? ValidateExpense(Expense expense)
    {
        if (expense.Amount <= 0)
            return "Amount must be greater than zero";

        if (expense.CategoryId <= 0)
            return "A valid category is required";

        if (expense.UserId <= 0)
            return "A valid user is required";

        if (expense.Date > DateTime.UtcNow)
            return "Expense date cannot be in the future";

        return null;
    }
}
