using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

public class ExpenseManager(IExpenseEngine engine, IExpenseAccessor accessor) : IExpenseManager
{
    public async Task<Result<Expense>> GetByIdAsync(int id)
    {
        var expense = await accessor.GetByIdAsync(id);
        return expense is not null
            ? Result<Expense>.Success(expense)
            : Result<Expense>.Failure("Expense not found");
    }

    public async Task<Result<IEnumerable<Expense>>> GetByUserIdAsync(int userId)
    {
        var expenses = await accessor.GetByUserIdAsync(userId);
        return Result<IEnumerable<Expense>>.Success(expenses);
    }

    public async Task<Result<int>> CreateAsync(Expense expense)
    {
        var error = engine.ValidateExpense(expense);
        if (error is not null)
            return Result<int>.Failure(error);

        var id = await accessor.CreateAsync(expense);
        return Result<int>.Success(id);
    }

    public async Task<Result<bool>> UpdateAsync(Expense expense)
    {
        var error = engine.ValidateExpense(expense);
        if (error is not null)
            return Result<bool>.Failure(error);

        var updated = await accessor.UpdateAsync(expense);
        return updated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Expense not found");
    }

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var deleted = await accessor.DeleteAsync(id);
        return deleted
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Expense not found");
    }
}
