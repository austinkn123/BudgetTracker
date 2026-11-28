using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>Pure rules for site feedback: normalizing and validating an entry, the per-user rate limit, and page size.</summary>
public interface IFeedbackEngine
{
    /// <summary>The message as stored: trimmed, or empty when null.</summary>
    string NormalizeMessage(string? message);

    /// <summary>The page as stored: a trimmed app path starting with "/", or null when absent or not a path.</summary>
    string? NormalizePage(string? page);

    /// <summary>An error message when the entry is invalid; otherwise null. Expects already-normalized fields.</summary>
    string? Validate(Feedback feedback);

    /// <summary><see cref="FeedbackErrors.RateLimited"/> when the user has hit the hourly or daily cap; otherwise null.</summary>
    string? CheckRateLimit(int sentLastHour, int sentLastDay);

    /// <summary>The requested page size clamped to 1 through <see cref="FeedbackLimits.MaxPageSize"/>; the max when absent.</summary>
    int ClampPageSize(int? take);
}
