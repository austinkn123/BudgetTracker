namespace BudgetTracker.Domain.Interfaces.Utilities;

/// <summary>
/// Resolves the currently authenticated user's identifiers from the request context.
/// </summary>
public interface ICurrentUserProvider
{
    /// <summary>The internal BudgetTracker user id (auto-provisioned on first call).</summary>
    int UserId { get; }

    /// <summary>The Cognito subject claim — stable external identifier safe to hand to third parties (e.g. Plaid's client_user_id).</summary>
    string CognitoSub { get; }
}
