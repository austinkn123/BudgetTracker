using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>
/// CRUD for BudgetTracker <see cref="Account"/> rows. Currently exposes only the operations
/// PlaidManager needs; expand as additional callers appear.
/// </summary>
public interface IAccountAccessor
{
    /// <summary>List all accounts owned by the user.</summary>
    Task<IReadOnlyList<Account>> GetByUserIdAsync(int userId);

    /// <summary>Insert a new account and return its id.</summary>
    Task<int> CreateAsync(Account account);
}
