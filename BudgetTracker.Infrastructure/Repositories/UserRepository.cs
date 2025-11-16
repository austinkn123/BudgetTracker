using BudgetTracker.Application.Interfaces;
using BudgetTracker.Core.Models;
using BudgetTracker.Infrastructure.Data;
using Dapper;

namespace BudgetTracker.Infrastructure.Repositories
{
    public class UserRepository(DapperContext _context) : IUserRepository
    {
        public async Task<int> CreateAsync(User user)
        {
            var sql = "INSERT INTO Users (CognitoUserId, Email) VALUES (@CognitoUserId, @Email); SELECT CAST(SCOPE_IDENTITY() as int)";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleAsync<int>(sql, user);
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var sql = "DELETE FROM Users WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
                return affectedRows > 0;
            }
        }

        public async Task<User> GetByCognitoIdAsync(string cognitoId)
        {
            var sql = "SELECT * FROM Users WHERE CognitoUserId = @CognitoId";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleOrDefaultAsync<User>(sql, new { CognitoId = cognitoId });
            }
        }

        public async Task<User> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM Users WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
            }
        }

        public async Task<bool> UpdateAsync(User user)
        {
            var sql = "UPDATE Users SET Email = @Email WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                var affectedRows = await connection.ExecuteAsync(sql, user);
                return affectedRows > 0;
            }
        }
    }
}
