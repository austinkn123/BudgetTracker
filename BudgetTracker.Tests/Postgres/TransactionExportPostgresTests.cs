using System.Diagnostics;
using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Tests.Postgres;

/// <summary>The CSV export's row stream (BUD-25) against real Postgres: the feed's filter, order and per-user scope.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class TransactionExportPostgresTests(PostgresFixture fixture)
{
    private async Task<List<TransactionExportRow>> ExportAsync(int userId, TransactionFilter filter)
    {
        await using var context = fixture.CreateContext();
        var rows = new List<TransactionExportRow>();
        await foreach (var row in new TransactionAccessor(context).StreamForExportAsync(userId, filter))
            rows.Add(row);
        return rows;
    }

    [PostgresFact]
    public async Task Should_StreamOnlyTheUsersFilteredRows_NewestFirst_WithCategoryAndAccountNames()
    {
        var user = await fixture.UserAsync();
        var other = await fixture.UserAsync();
        var checking = await fixture.AccountAsync(user.Id, "Chase Checking ••1234");
        var otherAccount = await fixture.AccountAsync(other.Id);
        var coffee = await fixture.CategoryAsync(user.Id, "Coffee");
        await fixture.TransactionsAsync(
            PostgresSeed.Row(checking.Id, new DateTime(2026, 11, 2), -4.5m, payee: "Blue Bottle", categoryId: coffee.Id, notes: "oat"),
            PostgresSeed.Row(checking.Id, new DateTime(2026, 11, 9), -18m, payee: "Uber", pending: true),
            PostgresSeed.Row(checking.Id, new DateTime(2026, 10, 30), -7m, payee: "October row"),
            PostgresSeed.Row(otherAccount.Id, new DateTime(2026, 11, 5), -99m, payee: "Someone else"));

        var rows = await ExportAsync(user.Id, new TransactionFilter(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)));

        Assert.Equal(
            [
                new TransactionExportRow(new DateTime(2026, 11, 9), "Uber", -18m, "Expense", null, "Chase Checking ••1234", true, null, null),
                new TransactionExportRow(new DateTime(2026, 11, 2), "Blue Bottle", -4.5m, "Expense", "Coffee", "Chase Checking ••1234", false, "oat", null),
            ],
            rows);
    }

    [PostgresFact]
    public async Task Should_ApplyTheFeedsSearchAndStatusFilters()
    {
        var user = await fixture.UserAsync();
        var checking = await fixture.AccountAsync(user.Id);
        await fixture.TransactionsAsync(
            PostgresSeed.Row(checking.Id, new DateTime(2026, 11, 2), payee: "Uber trip"),
            PostgresSeed.Row(checking.Id, new DateTime(2026, 11, 3), payee: "Uber Eats", pending: true),
            PostgresSeed.Row(checking.Id, new DateTime(2026, 11, 4), payee: "Lyft"));

        var rows = await ExportAsync(user.Id, new TransactionFilter(Search: "uber", IsPending: false));

        Assert.Equal(["Uber trip"], rows.Select(r => r.Payee));
    }

    [PostgresFact]
    public async Task Should_StreamNothing_When_NoRowMatches()
    {
        var user = await fixture.UserAsync();

        Assert.Empty(await ExportAsync(user.Id, new TransactionFilter()));
    }

    [PostgresFact]
    public async Task Should_Export10000RowsInUnderFiveSeconds()
    {
        var user = await fixture.UserAsync();
        var checking = await fixture.AccountAsync(user.Id);
        await fixture.TransactionsAsync(Enumerable.Range(0, 10_000)
            .Select(i => PostgresSeed.Row(checking.Id, new DateTime(2026, 1, 1).AddMinutes(i), -(i + 1), payee: $"Payee, \"{i}\""))
            .ToArray());
        var engine = new TransactionExportEngine();

        var clock = Stopwatch.StartNew();
        var lines = 0;
        await using (var context = fixture.CreateContext())
        {
            await foreach (var row in new TransactionAccessor(context).StreamForExportAsync(user.Id, new TransactionFilter()))
            {
                _ = engine.FormatRow(row);
                lines++;
            }
        }
        clock.Stop();

        Assert.Equal(10_000, lines);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"took {clock.Elapsed}");
    }
}
