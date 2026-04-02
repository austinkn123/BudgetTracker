using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

public class ExpenseAccessor(BudgetTrackerDbContext context) : IExpenseAccessor
{
    public async Task<int> CreateAsync(Expense expense)
    {
        context.Expenses.Add(expense);
        await context.SaveChangesAsync();
        return expense.Id;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var expense = await context.Expenses.FindAsync(id);
        if (expense is null) return false;

        context.Expenses.Remove(expense);
        return await context.SaveChangesAsync() > 0;
    }

    public async Task<Expense> GetByIdAsync(int id)
    {
        return await context.Expenses
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<IEnumerable<Expense>> GetByUserIdAsync(int userId)
    {
        return await context.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync();
    }

    public async Task<bool> UpdateAsync(Expense expense)
    {
        context.Expenses.Update(expense);
        return await context.SaveChangesAsync() > 0;
    }
}
