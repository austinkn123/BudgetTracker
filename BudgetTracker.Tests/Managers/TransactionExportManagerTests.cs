using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class TransactionExportManagerTests
{
    private readonly Mock<ITransactionAccessor> _accessor = new(MockBehavior.Strict);

    private static async IAsyncEnumerable<TransactionExportRow> Rows(params TransactionExportRow[] rows)
    {
        foreach (var row in rows)
        {
            await Task.Yield();
            yield return row;
        }
    }

    private static async Task<List<string>> ToListAsync(IAsyncEnumerable<string> lines)
    {
        var list = new List<string>();
        await foreach (var line in lines) list.Add(line);
        return list;
    }

    [Fact]
    public async Task Should_WriteTheHeaderThenOneLinePerRow_ForTheUsersFilter()
    {
        var filter = new TransactionFilter(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), Search: "uber");
        _accessor.Setup(a => a.StreamForExportAsync(5, filter)).Returns(Rows(
            new TransactionExportRow(new DateTime(2026, 11, 9), "Uber", -18m, "Expense", "Rideshare", "Checking ••1", false, null, null)));

        var export = new TransactionExportManager(new TransactionExportEngine(), _accessor.Object).ExportCsv(5, filter);

        Assert.Equal("budgettracker-transactions-2026-11-01-to-2026-11-30.csv", export.FileName);
        Assert.Equal(
            ["Date,Payee,Amount,Type,Category,Account,Pending,Notes,Plaid category", "2026-11-09,Uber,-18.00,Expense,Rideshare,Checking ••1,false,,"],
            await ToListAsync(export.Lines));
    }

    [Fact]
    public async Task Should_WriteOnlyTheHeader_When_NothingMatches()
    {
        _accessor.Setup(a => a.StreamForExportAsync(5, It.IsAny<TransactionFilter>())).Returns(Rows());

        var export = new TransactionExportManager(new TransactionExportEngine(), _accessor.Object).ExportCsv(5, new TransactionFilter());

        Assert.Equal(["Date,Payee,Amount,Type,Category,Account,Pending,Notes,Plaid category"], await ToListAsync(export.Lines));
    }
}
