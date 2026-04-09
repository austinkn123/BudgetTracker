using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface ITransactionEngine
{
    string? ValidateTransaction(Transaction transaction);
}
