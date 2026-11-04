using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models.MoneyMap;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>Orchestrates the money map: fetch the user's plans, ledger and categories, hand them to the engine.</summary>
public interface IMoneyMapManager
{
    /// <summary>The money map over the last <paramref name="months"/> months; a failure when that is outside 1–24.</summary>
    Task<Result<MoneyMap>> GetAsync(int userId, int months);
}
