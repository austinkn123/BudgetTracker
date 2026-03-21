using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface IUserEngine
{
    string? ValidateUser(User user);
}
