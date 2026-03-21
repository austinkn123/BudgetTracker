using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface IUserAccessor
{
    Task<User> GetByIdAsync(int id);
    Task<User> GetByCognitoIdAsync(string cognitoId);
    Task<int> CreateAsync(User user);
    Task<bool> UpdateAsync(User user);
    Task<bool> DeleteAsync(int id);
}
