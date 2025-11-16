using BudgetTracker.Application.Interfaces;
using BudgetTracker.Core.Models;
using BudgetTracker.Infrastructure.Data;
using Dapper;

namespace BudgetTracker.Infrastructure.Repositories
{
    public class ExpenseRepository(DapperContext _context) : IExpenseRepository
    {
        public async Task<int> CreateAsync(Expense expense)
        {
            var sql = "INSERT INTO Expenses (UserId, CategoryId, Amount, Date, Merchant, Notes) VALUES (@UserId, @CategoryId, @Amount, @Date, @Merchant, @Notes); SELECT CAST(SCOPE_IDENTITY() as int)";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleAsync<int>(sql, expense);
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var sql = "DELETE FROM Expenses WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
                return affectedRows > 0;
            }
        }

        public async Task<Expense> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM Expenses WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleOrDefaultAsync<Expense>(sql, new { Id = id });
            }
        }

        public async Task<IEnumerable<Expense>> GetByUserIdAsync(int userId)
        {
            var sql = "SELECT * FROM Expenses WHERE UserId = @UserId";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QueryAsync<Expense>(sql, new { UserId = userId });
            }
        }

        public async Task<bool> UpdateAsync(Expense expense)
        {
            var sql = "UPDATE Expenses SET CategoryId = @CategoryId, Amount = @Amount, Date = @Date, Merchant = @Merchant, Notes = @Notes WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                var affectedRows = await connection.ExecuteAsync(sql, expense);
                return affectedRows > 0;
            }
        }
    }
}
