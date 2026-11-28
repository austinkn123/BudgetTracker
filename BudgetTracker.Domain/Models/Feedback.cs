namespace BudgetTracker.Domain.Models;

/// <summary>A comment or critique a signed-in user sent about the site, read by the owner on the admin page.</summary>
public class Feedback
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>What the user wrote, trimmed. Never logged.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The app path the user was on when they sent it (e.g. <c>/budget</c>), or null.</summary>
    public string? Page { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>Limits on a <see cref="Feedback"/> entry. The client mirrors them in <c>feedbackSchema.ts</c>.</summary>
public static class FeedbackLimits
{
    public const int MaxMessageLength = 2000;
    public const int MaxPageLength = 200;

    /// <summary>Most entries one user may send in a rolling hour.</summary>
    public const int MaxPerHour = 5;

    /// <summary>Most entries one user may send in a rolling day.</summary>
    public const int MaxPerDay = 20;

    /// <summary>Most entries the admin list returns in one page.</summary>
    public const int MaxPageSize = 100;
}

/// <summary>Errors the feedback endpoints map to a status code other than 400.</summary>
public static class FeedbackErrors
{
    public const string RateLimited = "You've sent a lot of feedback recently. Please try again later.";
}
