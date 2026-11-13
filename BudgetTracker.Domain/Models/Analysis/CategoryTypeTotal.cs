namespace BudgetTracker.Domain.Models.Analysis;

/// <summary>
/// The signed sum of one category's rows of one type over a window, aggregated in the database. Outflows are negative
/// and refunds (positive Expense rows) are already netted in, so no business rule has been applied yet.
/// </summary>
/// <param name="CategoryId">The category; null means uncategorized.</param>
/// <param name="TransactionType">Income, Expense or Transfer.</param>
/// <param name="Amount">Sum of the rows' signed amounts.</param>
public record CategoryTypeTotal(int? CategoryId, string TransactionType, decimal Amount);
