namespace BudgetTracker.Domain.Interfaces.Utilities;

public interface ICurrentUserProvider
{
    int UserId { get; }
}
