using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

public interface IUserManager
{
    Task<Result<User>> GetByIdAsync(int id);
    Task<Result<User>> GetByCognitoIdAsync(string cognitoId);
    Task<Result<int>> CreateAsync(User user);
    Task<Result<bool>> UpdateAsync(User user);
    Task<Result<bool>> DeleteAsync(int id);
}
