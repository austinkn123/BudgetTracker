using System.Security.Claims;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;

namespace BudgetTracker.Server.Utilities;

public class CognitoCurrentUserProvider(IHttpContextAccessor contextAccessor, IUserManager userManager) : ICurrentUserProvider
{
    private int? _userId;
    private string? _cognitoSub;

    public int UserId
    {
        get
        {
            EnsureResolved();
            return _userId!.Value;
        }
    }

    public string CognitoSub
    {
        get
        {
            EnsureResolved();
            return _cognitoSub!;
        }
    }

    private void EnsureResolved()
    {
        if (_userId.HasValue)
            return;

        var context = contextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is not available");
        var user = context.User;

        var sub = user.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Cognito sub claim not found");
        var email = user.FindFirst("email")?.Value ?? throw new InvalidOperationException("Email claim not found");

        var result = userManager.GetOrProvisionByCognitoSubAsync(sub, email).GetAwaiter().GetResult();
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Failed to provision user: {result.Error}");

        _userId = result.Value;
        _cognitoSub = sub;
    }
}
