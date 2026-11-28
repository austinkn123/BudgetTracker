using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>A feedback entry as the admin page shows it.</summary>
public record FeedbackView(int Id, int UserId, string Message, string? Page, DateTime CreatedAt);

/// <summary>Workflows for site feedback: users send it, the admin reads it.</summary>
public interface IFeedbackManager
{
    /// <summary>
    /// Normalizes and validates the entry, enforces the per-user rate limit, then saves it and returns its id.
    /// Fails with <see cref="FeedbackErrors.RateLimited"/> when the user is over the limit.
    /// </summary>
    Task<Result<int>> SubmitAsync(Feedback feedback);

    /// <summary>Every user's entries, newest first, paged by id. Callers must already be authorized as admin.</summary>
    Task<Result<IReadOnlyList<FeedbackView>>> GetPageAsync(int? beforeId, int? take);
}
