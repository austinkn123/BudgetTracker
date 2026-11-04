using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.MoneyMap;

namespace BudgetTracker.Tests.Engines;

public class MoneyMapEngineTests
{
    private readonly MoneyMapEngine _sut = new();

    private static readonly DateTime Today = new(2026, 10, 30);

    // Category ids: 1 pay, 2 groceries, 3 car insurance, 4 registration, 5 shopping (unplanned), 6 HOA.
    private static readonly Category[] Categories =
    [
        new() { Id = 1, Name = "Net Income", CategoryType = "Income" },
        new() { Id = 2, Name = "Groceries" },
        new() { Id = 3, Name = "Car insurance" },
        new() { Id = 4, Name = "Car registration" },
        new() { Id = 5, Name = "Shopping" },
        new() { Id = 6, Name = "HOA" },
    ];

    private static int _nextId = 1;

    private static Transaction Row(int? categoryId, decimal amount, DateTime on, string type = "Expense", string? payee = null) => new()
    {
        Id = _nextId++,
        CategoryId = categoryId,
        Amount = amount,
        OccurredAt = on,
        TransactionType = type,
        Payee = payee
    };

    private static BudgetPlanEntry Line(int categoryId, decimal amount, string cadence = "Monthly", string bucket = "Core", string lineType = "Expense", int sort = 0) => new()
    {
        CategoryId = categoryId,
        LineType = lineType,
        Bucket = bucket,
        Cadence = cadence,
        Amount = amount,
        MonthlyEquivalent = cadence == "Annual" ? Math.Round(amount / 12m, 2) : amount,
        SortOrder = sort
    };

    private static BudgetPlan Plan(params BudgetPlanEntry[] entries) => new()
    {
        Name = "Recommended Budget",
        PlanMonth = new DateTime(2026, 11, 1),
        IsActive = true,
        NetIncomeMonthly = 6526m,
        Entries = entries
    };

    private MoneyMap Build(BudgetPlan? plan, IReadOnlyList<Transaction> rows, int months = 12) =>
        _sut.Build(plan is null ? [] : [plan], rows, Categories, Today, months).Value!;

    [Fact]
    public void Lines_FollowThePlanThenUnplannedSpend_EachAveragedOverTheWindow()
    {
        var plan = Plan(Line(2, 425m, sort: 2), Line(1, 6526m, lineType: "Income", sort: 0), Line(6, 916m, "Annual", sort: 1));
        var rows = new[]
        {
            Row(2, -1200m, new DateTime(2026, 3, 1), payee: "H-E-B"),
            Row(2, -600m, new DateTime(2026, 9, 1), payee: "H Mart"),
            Row(2, 60m, new DateTime(2026, 9, 2), payee: "H-E-B"),      // refund nets against spend
            Row(5, -240m, new DateTime(2026, 6, 1), payee: "Amazon"),   // unplanned
            Row(null, -120m, new DateTime(2026, 6, 1)),                 // uncategorized
            Row(2, -999m, new DateTime(2025, 10, 31)),                  // before the window
            Row(5, -500m, new DateTime(2026, 6, 1), type: "Adjustment") // money moved, not spent
        };

        var map = Build(plan, rows);

        Assert.Equal(new DateTime(2025, 11, 1), map.From);
        Assert.Equal(Today, map.To);
        Assert.Collection(map.Lines,
            hoa => Assert.Equal(("HOA", "Annual", 76.33m, 0m), (hoa.Name, hoa.Cadence, hoa.PlanMonthly, hoa.ActualMonthly)),
            groceries =>
            {
                Assert.Equal(("Groceries", "Core", 425m, 145m), (groceries.Name, groceries.Bucket, groceries.PlanMonthly, groceries.ActualMonthly));
                Assert.Equal(["H-E-B", "H Mart"], groceries.TopPayees);
            },
            shopping => Assert.Equal(("Shopping", null, 0m, 20m), (shopping.Name, shopping.Bucket, shopping.PlanMonthly, shopping.ActualMonthly)),
            uncategorized => Assert.Equal((null, "Uncategorized", 10m), (uncategorized.CategoryId, uncategorized.Name, uncategorized.ActualMonthly)));
    }

    [Fact]
    public void Income_ComesFromTheNewestActivePlan_AndActualsAverageOverTheWindow()
    {
        var older = Plan();
        older.Name = "Baseline";
        older.PlanMonth = new DateTime(2026, 4, 1);
        older.NetIncomeMonthly = 5646m;
        var inactiveNewer = Plan();
        inactiveNewer.Name = "Draft";
        inactiveNewer.PlanMonth = new DateTime(2026, 12, 1);
        inactiveNewer.IsActive = false;
        var current = Plan();

        var map = _sut.Build([older, inactiveNewer, current], [Row(1, 3000m, new DateTime(2026, 10, 2), "Income")], Categories, Today, 3).Value!;

        Assert.Equal("Recommended Budget", map.PlanName);
        Assert.Equal(6526m, map.PlannedIncomeMonthly);
        Assert.Equal(1000m, map.ActualIncomeMonthly);
        Assert.Equal(new DateTime(2026, 8, 1), map.From);
    }

    [Fact]
    public void NoPlan_StillMapsSpending()
    {
        var map = Build(null, [Row(5, -120m, new DateTime(2026, 10, 1), payee: "Target")]);

        Assert.Null(map.PlanName);
        Assert.Equal(0m, map.PlannedIncomeMonthly);
        Assert.Equal(10m, Assert.Single(map.Lines).ActualMonthly);
        Assert.Empty(map.Upcoming);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void Months_OutsideOneToTwentyFour_IsAFailure(int months)
    {
        Assert.False(_sut.Build([], [], Categories, Today, months).IsSuccess);
    }

    [Fact]
    public void Bills_PaidTwiceAboutSixMonthsApart_RecurEverySixMonthsAtTheLatestAmount()
    {
        // Switched carriers in October: Clearcover in May, Progressive in October.
        var plan = Plan(Line(3, 1886m, "Annual"));
        var rows = new[]
        {
            Row(3, -1878m, new DateTime(2026, 5, 5), payee: "Clearcover"),
            Row(3, -943m, new DateTime(2026, 10, 8), payee: "Progressive"),
            Row(3, -12m, new DateTime(2026, 7, 1), payee: "Policy fee")   // too small to be the bill itself
        };

        var bills = Build(plan, rows).Upcoming;

        Assert.Equal(
            [(new DateTime(2027, 4, 8), 943m), (new DateTime(2027, 10, 8), 943m)],
            bills.Select(b => (b.Date, b.Amount)));
        Assert.All(bills, b => Assert.Equal(("Car insurance", UpcomingKind.Bill), (b.Label, b.Kind)));
    }

    [Fact]
    public void Bills_PaidOnceOrMonthsApart_EachRecurAYearLater_AndOnlyWhenStillAhead()
    {
        var plan = Plan(Line(4, 283m, "Annual"), Line(6, 916m, "Annual"));
        var rows = new[]
        {
            Row(4, -207m, new DateTime(2025, 11, 15)),
            Row(4, -76m, new DateTime(2026, 9, 14)),
            Row(6, -916m, new DateTime(2026, 1, 13)),
            Row(6, -900m, new DateTime(2025, 10, 1))   // over a year ago: already renewed
        };

        var bills = Build(plan, rows).Upcoming;

        Assert.Equal(
            [(new DateTime(2026, 11, 15), "Car registration", 207m), (new DateTime(2027, 1, 13), "HOA", 916m), (new DateTime(2027, 9, 14), "Car registration", 76m)],
            bills.Select(b => (b.Date, b.Label, b.Amount)));
    }

    [Fact]
    public void ExtraPaychecks_AreTheMonthsAheadWithAThirdBiweeklyPayday()
    {
        var plan = Plan(Line(1, 6526m, lineType: "Income"));
        var rows = new[]
        {
            Row(1, 3043.63m, new DateTime(2026, 9, 18), "Income"),
            Row(1, 3043.63m, new DateTime(2026, 10, 2), "Income"),
            Row(1, 3043.62m, new DateTime(2026, 10, 16), "Income"),
            Row(1, 166m, new DateTime(2026, 10, 20), "Income"),       // a tax refund between paychecks
            Row(1, 3262.80m, new DateTime(2026, 10, 30), "Income")    // includes a one-off travel reimbursement
        };

        var extras = Build(plan, rows).Upcoming;

        // October 2026 already had its third (Oct 2, 16, 30), so the next are April and October 2027, at the usual
        // paycheck rather than the latest one's one-off bump.
        Assert.Equal(
            [(new DateTime(2027, 4, 30), 3043.63m), (new DateTime(2027, 10, 29), 3043.63m)],
            extras.Select(e => (e.Date, e.Amount)));
        Assert.All(extras, e => Assert.Equal(UpcomingKind.ExtraPaycheck, e.Kind));
    }

    [Fact]
    public void ExtraPaychecks_NoneForMonthlyPay()
    {
        var plan = Plan(Line(1, 6526m, lineType: "Income"));
        var rows = new[] { Row(1, 6526m, new DateTime(2026, 9, 30), "Income"), Row(1, 6526m, new DateTime(2026, 10, 30), "Income") };

        Assert.Empty(Build(plan, rows).Upcoming);
    }
}
