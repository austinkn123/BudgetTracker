using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

public interface IUserAccessor
{
    Task<User> GetByIdAsync(int id);
    Task<User?> GetByCognitoSubAsync(string sub);
    Task<int> CreateAsync(User user);
    Task<bool> UpdateAsync(User user);
    Task<bool> DeleteAsync(int id);
}
