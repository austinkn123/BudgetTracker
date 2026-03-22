using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Engines;

public class UserEngine : IUserEngine
{
    public string? ValidateUser(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return "Email is required";

        return null;
    }
}
