using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Dapper;

namespace BudgetTracker.Domain.Accessors;

public class UserAccessor(DapperContext context) : IUserAccessor
{
    public async Task<int> CreateAsync(User user)
    {
        var sql = "INSERT INTO Users (Email) VALUES (@Email); SELECT CAST(SCOPE_IDENTITY() as int)";
        using var connection = context.CreateConnection();
        return await connection.QuerySingleAsync<int>(sql, user);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var sql = "DELETE FROM Users WHERE Id = @Id";
        using var connection = context.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(sql, new { Id = id });
        return affectedRows > 0;
    }

    public async Task<User> GetByIdAsync(int id)
    {
        var sql = "SELECT * FROM Users WHERE Id = @Id";
        using var connection = context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<bool> UpdateAsync(User user)
    {
        var sql = "UPDATE Users SET Email = @Email WHERE Id = @Id";
        using var connection = context.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(sql, user);
        return affectedRows > 0;
    }
}
