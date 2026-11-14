using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Tests.Engines;

public class SandboxSeedEngineTests
{
    private readonly SandboxSeedEngine _engine = new();

    private static BudgetPlan PlanWith(params BudgetPlanEntry[] entries) => new() { Id = 1, UserId = 5, IsActive = true, Entries = entries };

    private static BudgetPlanEntry Expense(int? categoryId, decimal monthly) =>
        new() { LineType = "Expense", CategoryId = categoryId, MonthlyEquivalent = monthly };

    // ── ValidateMonths ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void Should_AcceptMonths_When_WithinOneToTwelve(int months)
    {
        Assert.Null(_engine.ValidateMonths(months));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Should_RejectMonths_When_OutsideOneToTwelve(int months)
    {
        Assert.Equal("Months must be between 1 and 12", _engine.ValidateMonths(months));
    }

    // ── BuildLines ──────────────────────────────────────────────────────────

    [Fact]
    public void Should_SplitGroceriesAcrossEightHebPurchases()
    {
        var lines = _engine.BuildLines(PlanWith(Expense(1, 400m)), new Dictionary<int, string> { [1] = "Groceries" });

        Assert.Equal([new SandboxSeedLine("H-E-B", 400m, 8)], lines);
    }

    [Fact]
    public void Should_MatchKeywordAnywhereInNameIgnoringCase()
    {
        var lines = _engine.BuildLines(PlanWith(Expense(1, 600m)), new Dictionary<int, string> { [1] = "FOOD (Groceries + Eating Out)" });

        Assert.Equal([new SandboxSeedLine("H-E-B", 600m, 8)], lines);
    }

    [Fact]
    public void Should_EmitOneMortgagePayment_When_CategoryIsAFixedBill()
    {
        var lines = _engine.BuildLines(PlanWith(Expense(2, 1800m)), new Dictionary<int, string> { [2] = "Mortgage" });

        Assert.Equal([new SandboxSeedLine("First National Mortgage", 1800m, 1)], lines);
    }

    [Fact]
    public void Should_KeepCategoryName_When_NoKeywordMatches()
    {
        var lines = _engine.BuildLines(PlanWith(Expense(3, 50m)), new Dictionary<int, string> { [3] = "Pets" });

        Assert.Equal([new SandboxSeedLine("Pets", 50m, 1)], lines);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(99)]
    public void Should_UseGeneral_When_EntryHasNoKnownCategory(int? categoryId)
    {
        var lines = _engine.BuildLines(PlanWith(Expense(categoryId, 75m)), new Dictionary<int, string> { [1] = "Food" });

        Assert.Equal([new SandboxSeedLine("General", 75m, 1)], lines);
    }

    [Fact]
    public void Should_ExcludeIncomeAndNonPositiveEntries()
    {
        var plan = PlanWith(
            new BudgetPlanEntry { LineType = "Income", CategoryId = 1, MonthlyEquivalent = 5000m },
            Expense(1, 0m),
            Expense(1, -20m),
            Expense(2, 1800m));
        var names = new Dictionary<int, string> { [1] = "Food", [2] = "Rent" };

        var lines = _engine.BuildLines(plan, names);

        Assert.Equal([new SandboxSeedLine("First National Mortgage", 1800m, 1)], lines);
    }

    [Fact]
    public void Should_ReturnNoLines_When_PlanHasNoEntries()
    {
        Assert.Empty(_engine.BuildLines(PlanWith(), new Dictionary<int, string>()));
    }

    // ── BuildConfig ─────────────────────────────────────────────────────────

    [Fact]
    public void Should_BuildConfigWithEmployerDirectDeposit()
    {
        IReadOnlyList<SandboxSeedLine> lines = [new("H-E-B", 400m, 8)];
        var from = new DateOnly(2026, 9, 1);
        var through = new DateOnly(2026, 10, 15);

        var config = _engine.BuildConfig(lines, 5000m, from, through);

        Assert.Equal(SandboxSeedBuilder.Build(lines, 5000m, "Employer Direct Deposit", from, through), config);
    }

    // ── SelectItemToReplace ─────────────────────────────────────────────────

    private static PlaidItem Item(int id, DateTime createdAt) => new() { Id = id, UserId = 5, CreatedAt = createdAt };

    [Fact]
    public void Should_ReplaceMostRecentlyCreatedItem()
    {
        var items = new[] { Item(11, new DateTime(2026, 9, 1)), Item(12, new DateTime(2026, 9, 20)), Item(13, new DateTime(2026, 8, 1)) };

        Assert.Equal(12, _engine.SelectItemToReplace(items)?.Id);
    }

    [Fact]
    public void Should_ReplaceHighestId_When_CreatedAtTies()
    {
        var createdAt = new DateTime(2026, 9, 20, 8, 0, 0);

        Assert.Equal(14, _engine.SelectItemToReplace([Item(12, createdAt), Item(14, createdAt), Item(13, createdAt)])?.Id);
    }

    [Fact]
    public void Should_ReplaceNothing_When_UserHasNoActiveItems()
    {
        Assert.Null(_engine.SelectItemToReplace([]));
    }
}
