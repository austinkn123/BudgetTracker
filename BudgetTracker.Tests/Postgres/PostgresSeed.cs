using BudgetTracker.Domain.Models;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// Seeds rows for one test through its own short-lived context. Every test starts from a fresh user, which is what
/// keeps tests independent inside the shared database.
/// </summary>
internal static class PostgresSeed
{
    public static async Task<User> UserAsync(this PostgresFixture fixture, long dataVersion = 0)
    {
        var user = new User { CognitoSub = $"sub-{Guid.NewGuid():N}", DataVersion = dataVersion };
        await using var context = fixture.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public static async Task<Account> AccountAsync(this PostgresFixture fixture, int userId, string name = "Checking")
    {
        var account = new Account { UserId = userId, Name = name, AccountType = "Checking" };
        await using var context = fixture.CreateContext();
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public static async Task<Category> CategoryAsync(this PostgresFixture fixture, int userId, string name, string type = "Expense")
    {
        var category = new Category { UserId = userId, Name = name, CategoryType = type };
        await using var context = fixture.CreateContext();
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    /// <summary>Inserts the rows; their generated ids are set on the instances passed in.</summary>
    public static async Task TransactionsAsync(this PostgresFixture fixture, params Transaction[] rows)
    {
        await using var context = fixture.CreateContext();
        context.Transactions.AddRange(rows);
        await context.SaveChangesAsync();
    }

    public static Transaction Row(
        int accountId,
        DateTime occurredAt,
        decimal amount = -10m,
        string type = "Expense",
        int? categoryId = null,
        string? payee = null,
        string? notes = null,
        string? plaidAccountId = null,
        bool pending = false,
        bool imported = false,
        int? transferAccountId = null) =>
        new()
        {
            AccountId = accountId,
            OccurredAt = occurredAt,
            Amount = amount,
            TransactionType = type,
            CategoryId = categoryId,
            Payee = payee,
            Notes = notes,
            PlaidAccountId = plaidAccountId,
            IsPending = pending,
            IsImported = imported,
            TransferAccountId = transferAccountId
        };
}
