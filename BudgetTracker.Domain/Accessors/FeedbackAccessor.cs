using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

/// <inheritdoc />
public class FeedbackAccessor(BudgetTrackerDbContext context) : IFeedbackAccessor
{
    /// <inheritdoc />
    public async Task<int> CreateAsync(Feedback feedback)
    {
        context.Feedback.Add(feedback);
        await context.SaveChangesAsync();
        return feedback.Id;
    }

    /// <inheritdoc />
    public Task<int> CountSinceAsync(int userId, DateTime since) =>
        context.Feedback.CountAsync(f => f.UserId == userId && f.CreatedAt >= since);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Feedback>> GetPageAsync(int? beforeId, int take) =>
        await context.Feedback
            .AsNoTracking()
            .Where(f => beforeId == null || f.Id < beforeId)
            .OrderByDescending(f => f.Id)
            .Take(take)
            .ToListAsync();
}
