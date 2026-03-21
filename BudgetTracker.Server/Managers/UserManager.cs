using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

public class UserManager(IUserEngine engine, IUserAccessor accessor) : IUserManager
{
    public async Task<Result<User>> GetByIdAsync(int id)
    {
        var user = await accessor.GetByIdAsync(id);
        return user is not null
            ? Result<User>.Success(user)
            : Result<User>.Failure("User not found");
    }

    public async Task<Result<User>> GetByCognitoIdAsync(string cognitoId)
    {
        var user = await accessor.GetByCognitoIdAsync(cognitoId);
        return user is not null
            ? Result<User>.Success(user)
            : Result<User>.Failure("User not found");
    }

    public async Task<Result<int>> CreateAsync(User user)
    {
        var error = engine.ValidateUser(user);
        if (error is not null)
            return Result<int>.Failure(error);

        var id = await accessor.CreateAsync(user);
        return Result<int>.Success(id);
    }

    public async Task<Result<bool>> UpdateAsync(User user)
    {
        var error = engine.ValidateUser(user);
        if (error is not null)
            return Result<bool>.Failure(error);

        var updated = await accessor.UpdateAsync(user);
        return updated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("User not found");
    }

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var deleted = await accessor.DeleteAsync(id);
        return deleted
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("User not found");
    }
}
