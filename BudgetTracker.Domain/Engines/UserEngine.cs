using System.Text.RegularExpressions;
using BudgetTracker.Domain.Common;
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

    public Result ValidateProvisioning(string sub, string email)
    {
        if (string.IsNullOrWhiteSpace(sub))
            return Result.Failure("Cognito sub claim is required");

        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure("Email is required");

        if (!IsValidEmail(email))
            return Result.Failure("Invalid email format");

        return Result.Success();
    }

    private static bool IsValidEmail(string email)
    {
        const string emailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$";
        return Regex.IsMatch(email, emailPattern);
    }
}
