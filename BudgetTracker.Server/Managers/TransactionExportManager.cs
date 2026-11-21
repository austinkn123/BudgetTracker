using System.Runtime.CompilerServices;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

/// <inheritdoc />
public class TransactionExportManager(ITransactionExportEngine engine, ITransactionAccessor transactionAccessor) : ITransactionExportManager
{
    /// <inheritdoc />
    public CsvExport ExportCsv(int userId, TransactionFilter filter) =>
        new(engine.FileName(filter.From, filter.To), LinesAsync(userId, filter));

    private async IAsyncEnumerable<string> LinesAsync(int userId, TransactionFilter filter, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return engine.HeaderLine;
        await foreach (var row in transactionAccessor.StreamForExportAsync(userId, filter).WithCancellation(cancellationToken))
            yield return engine.FormatRow(row);
    }
}
