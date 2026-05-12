using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface IUserEngine
{
    string? ValidateUser(User user);
    Result ValidateProvisioning(string sub, string email);
}
