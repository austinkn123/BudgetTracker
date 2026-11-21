using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Managers;

/// <summary>A CSV download: its file name and its lines (header first), produced as they are read.</summary>
public record CsvExport(string FileName, IAsyncEnumerable<string> Lines);

/// <summary>The ledger as CSV (BUD-25).</summary>
public interface ITransactionExportManager
{
    /// <summary>The rows the feed shows for <paramref name="filter"/>, newest first, as CSV lines.</summary>
    CsvExport ExportCsv(int userId, TransactionFilter filter);
}
