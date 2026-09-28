using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Engines;

namespace BudgetTracker.Domain.Engines;

public class UserEngine : IUserEngine
{
    public Result ValidateProvisioning(string sub)
    {
        if (string.IsNullOrWhiteSpace(sub))
            return Result.Failure("Cognito sub claim is required");

        return Result.Success();
    }
}
