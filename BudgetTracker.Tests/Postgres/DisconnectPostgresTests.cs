using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// BUD-10's two writes on real PostgreSQL: the list of Plaid account ids translates to Npgsql's own SQL, and an empty
/// stored token satisfies the PlaidItems column constraints.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class DisconnectPostgresTests(PostgresFixture fixture)
{
    private static readonly DateTime Day = new(2026, 10, 1);

    [PostgresFact]
    public async Task Should_DeleteOnlyThatBanksImportedRows_AndKeepManualOnes()
    {
        var user = await fixture.UserAsync();
        var other = await fixture.UserAsync();
        var account = await fixture.AccountAsync(user.Id);
        var otherAccount = await fixture.AccountAsync(other.Id);
        await fixture.TransactionsAsync(
            Imported(PostgresSeed.Row(account.Id, Day, plaidAccountId: "bud10-chk", imported: true), "bud10-t1"),
            Imported(PostgresSeed.Row(account.Id, Day, plaidAccountId: "bud10-card", imported: true), "bud10-t2"),
            Imported(PostgresSeed.Row(account.Id, Day, plaidAccountId: "bud10-other-bank", imported: true), "bud10-t3"),
            PostgresSeed.Row(account.Id, Day, payee: "manual", plaidAccountId: "bud10-chk"),
            Imported(PostgresSeed.Row(otherAccount.Id, Day, plaidAccountId: "bud10-chk", imported: true), "bud10-t4"));

        await using (var context = fixture.CreateContext())
        {
            var deleted = await new TransactionAccessor(context).DeleteImportedByPlaidAccountIdsAsync(user.Id, ["bud10-chk", "bud10-card"]);
            Assert.Equal(2, deleted);
        }

        await using var check = fixture.CreateContext();
        var left = await check.Transactions.AsNoTracking()
            .Where(t => t.AccountId == account.Id || t.AccountId == otherAccount.Id)
            .Select(t => t.PlaidAccountId + (t.IsImported ? ":imported" : ":manual"))
            .ToListAsync();
        Assert.Equal(new[] { "bud10-chk:imported", "bud10-chk:manual", "bud10-other-bank:imported" }.Order(), left.Order());
    }

    [PostgresFact]
    public async Task Should_WipeTheStoredTokenWithinTheColumnConstraints()
    {
        var user = await fixture.UserAsync();
        var keyRing = new EphemeralDataProtectionProvider();
        int itemId;
        await using (var context = fixture.CreateContext())
        {
            itemId = await new PlaidItemAccessor(context, keyRing).AddAsync(
                user.Id, "access-sandbox-secret", $"item-{Guid.NewGuid():N}", "ins_3", "Chase", null,
                [new PlaidAccount { PlaidAccountId = $"acct-{Guid.NewGuid():N}", Name = "Checking", AccountType = "depository" }]);
        }

        IReadOnlyList<string>? accountIds;
        await using (var context = fixture.CreateContext())
            accountIds = await new PlaidItemAccessor(context, keyRing).DisconnectAsync(user.Id, itemId);

        Assert.Single(accountIds!);
        await using var check = fixture.CreateContext();
        var stored = await check.PlaidItems.AsNoTracking().SingleAsync(p => p.Id == itemId);
        Assert.False(stored.IsActive);
        Assert.Equal(string.Empty, stored.AccessTokenEncrypted);
        Assert.Null(stored.SyncCursor);
    }

    private static Transaction Imported(Transaction row, string plaidTransactionId)
    {
        row.PlaidTransactionId = plaidTransactionId;
        return row;
    }
}
