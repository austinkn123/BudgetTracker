using BudgetTracker.Domain.Common;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface IUserEngine
{
    Result ValidateProvisioning(string sub);
}
