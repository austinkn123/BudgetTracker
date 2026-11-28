using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Engines;

/// <inheritdoc />
public class FeedbackEngine : IFeedbackEngine
{
    /// <inheritdoc />
    public string NormalizeMessage(string? message) => message?.Trim() ?? string.Empty;

    /// <inheritdoc />
    public string? NormalizePage(string? page)
    {
        var trimmed = page?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith('/'))
            return null;

        // Keep the path only: a query string or fragment can carry tokens (e.g. Plaid's OAuth state).
        var end = trimmed.IndexOfAny(['?', '#']);
        if (end >= 0)
            trimmed = trimmed[..end];

        return trimmed.Length > FeedbackLimits.MaxPageLength ? trimmed[..FeedbackLimits.MaxPageLength] : trimmed;
    }

    /// <inheritdoc />
    public string? Validate(Feedback feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback.Message))
            return "Feedback can't be empty";
        if (feedback.Message.Length > FeedbackLimits.MaxMessageLength)
            return $"Feedback must be {FeedbackLimits.MaxMessageLength} characters or fewer";
        return null;
    }

    /// <inheritdoc />
    public string? CheckRateLimit(int sentLastHour, int sentLastDay) =>
        sentLastHour >= FeedbackLimits.MaxPerHour || sentLastDay >= FeedbackLimits.MaxPerDay ? FeedbackErrors.RateLimited : null;

    /// <inheritdoc />
    public int ClampPageSize(int? take) => Math.Clamp(take ?? FeedbackLimits.MaxPageSize, 1, FeedbackLimits.MaxPageSize);
}
