using BudgetTracker.Domain.Interfaces.Utilities;

namespace BudgetTracker.Server.Utilities;

/// <summary>
/// Single-user implementation. Replace with CognitoCurrentUserProvider
/// when multi-user auth is introduced.
/// </summary>
public class HardcodedCurrentUserProvider : ICurrentUserProvider
{
    public int UserId => 1;
}
