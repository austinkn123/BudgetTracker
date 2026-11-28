using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

/// <inheritdoc />
/// <param name="clock">Wall time for the rate-limit windows; defaults to <see cref="TimeProvider.System"/>.</param>
public class FeedbackManager(IFeedbackEngine engine, IFeedbackAccessor accessor, TimeProvider? clock = null) : IFeedbackManager
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <inheritdoc />
    public async Task<Result<int>> SubmitAsync(Feedback feedback)
    {
        feedback.Message = engine.NormalizeMessage(feedback.Message);
        feedback.Page = engine.NormalizePage(feedback.Page);
        if (engine.Validate(feedback) is { } error)
            return Result<int>.Failure(error);

        // Counted in the database, not in memory, so the limit holds across Lambda instances and cold starts.
        var now = _clock.GetUtcNow().UtcDateTime;
        var lastHour = await accessor.CountSinceAsync(feedback.UserId, now.AddHours(-1));
        var lastDay = await accessor.CountSinceAsync(feedback.UserId, now.AddDays(-1));
        if (engine.CheckRateLimit(lastHour, lastDay) is { } limited)
            return Result<int>.Failure(limited);

        feedback.CreatedAt = now;
        return Result<int>.Success(await accessor.CreateAsync(feedback));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<FeedbackView>>> GetPageAsync(int? beforeId, int? take)
    {
        var rows = await accessor.GetPageAsync(beforeId, engine.ClampPageSize(take));
        // Stored as UTC wall time; marked Utc so the JSON carries "Z" and the browser shows it in the viewer's zone.
        IReadOnlyList<FeedbackView> views = rows
            .Select(f => new FeedbackView(f.Id, f.UserId, f.Message, f.Page, DateTime.SpecifyKind(f.CreatedAt, DateTimeKind.Utc)))
            .ToList();
        return Result<IReadOnlyList<FeedbackView>>.Success(views);
    }
}
