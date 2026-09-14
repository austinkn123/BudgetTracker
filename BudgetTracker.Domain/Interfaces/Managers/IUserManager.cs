using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>
/// The client-facing profile. The email is projected from the Cognito token at request time,
/// not read from the database — Cognito is the source of truth for it.
/// </summary>
public record UserProfile(int Id, string Email, DateTime CreatedAt);

public interface IUserManager
{
    Task<Result<User>> GetByIdAsync(int id);
    Task<Result<int>> GetOrProvisionByCognitoSubAsync(string sub);
}
