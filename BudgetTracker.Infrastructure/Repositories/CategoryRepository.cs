using BudgetTracker.Application.Interfaces;
using BudgetTracker.Core.Models;
using BudgetTracker.Infrastructure.Data;
using Dapper;

namespace BudgetTracker.Infrastructure.Repositories
{
    public class CategoryRepository(DapperContext _context) : ICategoryRepository
    {
        public async Task<int> CreateAsync(Category category)
        {
            var sql = "INSERT INTO Categories (UserId, Name) VALUES (@UserId, @Name); SELECT CAST(SCOPE_IDENTITY() as int)";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleAsync<int>(sql, category);
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var sql = "DELETE FROM Categories WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
                return affectedRows > 0;
            }
        }

        public async Task<Category> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM Categories WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleOrDefaultAsync<Category>(sql, new { Id = id });
            }
        }

        public async Task<IEnumerable<Category>> GetByUserIdAsync(int userId)
        {
            var sql = "SELECT * FROM Categories WHERE UserId = @UserId";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QueryAsync<Category>(sql, new { UserId = userId });
            }
        }

        public async Task<bool> UpdateAsync(Category category)
        {
            var sql = "UPDATE Categories SET Name = @Name WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                var affectedRows = await connection.ExecuteAsync(sql, category);
                return affectedRows > 0;
            }
        }
    }
}
