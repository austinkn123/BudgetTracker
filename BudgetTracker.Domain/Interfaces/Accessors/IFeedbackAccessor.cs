using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Accessors;

/// <summary>Persistence for <see cref="Feedback"/> entries.</summary>
public interface IFeedbackAccessor
{
    /// <summary>Inserts the entry and returns its id.</summary>
    Task<int> CreateAsync(Feedback feedback);

    /// <summary>How many entries the user has sent at or after <paramref name="since"/> (UTC).</summary>
    Task<int> CountSinceAsync(int userId, DateTime since);

    /// <summary>
    /// Every user's entries, newest first: at most <paramref name="take"/>, and only those with an id below
    /// <paramref name="beforeId"/> when given (keyset paging).
    /// </summary>
    Task<IReadOnlyList<Feedback>> GetPageAsync(int? beforeId, int take);
}
