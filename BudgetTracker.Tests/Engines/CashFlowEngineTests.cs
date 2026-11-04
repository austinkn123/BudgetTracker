using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.CashFlow;

namespace BudgetTracker.Tests.Engines;

public class CashFlowEngineTests
{
    private readonly CashFlowEngine _sut = new();

    // A Tuesday, so the current week's Sunday (Oct 18) differs from today.
    private static readonly DateTime Today = new(2026, 10, 20);

    private static DateTime D(int month, int day, int year = 2026) => new(year, month, day);

    private static Transaction Txn(
        int id, string type, decimal amount, DateTime occurredAt,
        string? payee = null, int? categoryId = null, string? plaidAccountId = "chk", bool pending = false) => new()
    {
        Id = id,
        AccountId = 1,
        TransactionType = type,
        Amount = amount,
        OccurredAt = occurredAt,
        Payee = payee,
        CategoryId = categoryId,
        PlaidAccountId = plaidAccountId,
        IsPending = pending
    };

    private static Transaction Income(int id, decimal amount, DateTime on, string? payee = "Acme Payroll") => Txn(id, "Income", amount, on, payee);

    private static Transaction Expense(int id, decimal amount, DateTime on, int? categoryId = null, string? plaidAccountId = "chk") =>
        Txn(id, "Expense", amount, on, categoryId: categoryId, plaidAccountId: plaidAccountId);

    private static PlaidAccount Account(string plaidAccountId, string type, decimal? balance) => new()
    {
        PlaidAccountId = plaidAccountId,
        Name = plaidAccountId,
        AccountType = type,
        CurrentBalance = balance
    };

    private CashFlowReport Build(CashFlowQuery query, IReadOnlyList<Transaction> transactions, IReadOnlyList<PlaidAccount>? accounts = null)
    {
        var result = _sut.BuildReport(query, transactions, accounts ?? [], Today);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    // ── Buckets ─────────────────────────────────────────────────────────────

    [Fact]
    public void Week_StartsOnSundayAndZeroFills()
    {
        var report = Build(new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Week), [Expense(1, -40m, D(10, 5))]);

        Assert.Equal([D(9, 27), D(10, 4), D(10, 11), D(10, 18)], report.Periods.Select(p => p.Start));
        Assert.Equal([0m, 40m, 0m, 0m], report.Periods.Select(p => p.MoneyOut));
    }

    [Fact]
    public void Week_SpanningNewYear_IsOneSundayStartedBucket()
    {
        // Dec 28 2025 is a Sunday: Dec 31 and Jan 3 share its week; Jan 4 starts the next.
        var report = Build(
            new CashFlowQuery(D(12, 30, 2025), D(1, 10), CashFlowGrouping.Week),
            [Expense(1, -10m, D(12, 31, 2025)), Expense(2, -20m, D(1, 3)), Expense(3, -5m, D(1, 4))]);

        Assert.Equal([D(12, 28, 2025), D(1, 4)], report.Periods.Select(p => p.Start));
        Assert.Equal([30m, 5m], report.Periods.Select(p => p.MoneyOut));
    }

    [Fact]
    public void Week_AcrossDaylightSavingChanges_KeepsSundayBoundaries()
    {
        // US clocks change on Sun Mar 8 and Sun Nov 1 2026; a late-evening row on those days stays in their week.
        var spring = Build(
            new CashFlowQuery(D(3, 1), D(3, 21), CashFlowGrouping.Week),
            [Expense(1, -10m, D(3, 7).AddHours(23).AddMinutes(30)), Expense(2, -20m, D(3, 8).AddHours(23).AddMinutes(30))]);
        var fall = Build(
            new CashFlowQuery(D(10, 25), D(11, 14), CashFlowGrouping.Week),
            [Expense(1, -10m, D(10, 31).AddHours(23).AddMinutes(30)), Expense(2, -20m, D(11, 1).AddHours(23).AddMinutes(30))]);

        Assert.Equal([D(3, 1), D(3, 8), D(3, 15)], spring.Periods.Select(p => p.Start));
        Assert.Equal([10m, 20m, 0m], spring.Periods.Select(p => p.MoneyOut));
        Assert.Equal([D(10, 25), D(11, 1), D(11, 8)], fall.Periods.Select(p => p.Start));
        Assert.Equal([10m, 20m, 0m], fall.Periods.Select(p => p.MoneyOut));
    }

    [Theory]
    [InlineData(CashFlowGrouping.Week)]
    [InlineData(CashFlowGrouping.Month)]
    [InlineData(CashFlowGrouping.Year)]
    public void FromMidBucket_RowsInThatBucketBeforeFrom_DoNotCount(CashFlowGrouping groupBy)
    {
        // Oct 15 is a Thursday; Oct 13 shares its week, month and year but is outside the window.
        var report = Build(
            new CashFlowQuery(D(10, 15), Today, groupBy),
            [Expense(1, -70m, D(10, 13)), Expense(2, -40m, D(10, 16)), Income(3, 900m, D(10, 14))]);

        Assert.Equal(40m, report.Periods[0].MoneyOut);
        Assert.Equal(0m, report.Periods[0].MoneyIn);
        Assert.Equal(new CashFlowTotals(0m, 40m), report.Totals);
    }

    [Fact]
    public void Month_ZeroFillsGapsOldestFirst()
    {
        var report = Build(
            new CashFlowQuery(D(7, 1), Today, CashFlowGrouping.Month),
            [Income(1, 100m, D(10, 2)), Income(2, 50m, D(7, 31))]);

        Assert.Equal([D(7, 1), D(8, 1), D(9, 1), D(10, 1)], report.Periods.Select(p => p.Start));
        Assert.Equal([50m, 0m, 0m, 100m], report.Periods.Select(p => p.MoneyIn));
    }

    [Fact]
    public void Day_HasOnePeriodPerCalendarDay()
    {
        var report = Build(new CashFlowQuery(D(10, 18), Today, CashFlowGrouping.Day), []);

        Assert.Equal([D(10, 18), D(10, 19), D(10, 20)], report.Periods.Select(p => p.Start));
    }

    [Fact]
    public void Year_StartsOnJanuaryFirst()
    {
        var report = Build(new CashFlowQuery(D(3, 1, 2024), Today, CashFlowGrouping.Year), []);

        Assert.Equal([D(1, 1, 2024), D(1, 1, 2025), D(1, 1, 2026)], report.Periods.Select(p => p.Start));
    }

    [Fact]
    public void NullFrom_StartsAtTheEarliestTransaction()
    {
        var report = Build(
            new CashFlowQuery(null, Today, CashFlowGrouping.Month),
            [Expense(1, -10m, D(10, 2)), Expense(2, -5m, D(8, 15))]);

        Assert.Equal([D(8, 1), D(9, 1), D(10, 1)], report.Periods.Select(p => p.Start));
        Assert.Equal(15m, report.Totals.MoneyOut);
    }

    [Fact]
    public void NullFrom_NoTransactions_IsEmptyWithZeroTotals()
    {
        var report = Build(new CashFlowQuery(null, Today, CashFlowGrouping.Month), []);

        Assert.Empty(report.Periods);
        Assert.Equal(new CashFlowTotals(0m, 0m), report.Totals);
        Assert.Equal(new CashFlowTotals(0m, 0m), report.Pending);
        Assert.Empty(report.IncomeSources);
        Assert.Empty(report.SpendByCategory);
    }

    [Fact]
    public void FromAfterTo_IsRejected()
    {
        var result = _sut.BuildReport(new CashFlowQuery(D(10, 21), D(10, 20), CashFlowGrouping.Day), [], [], Today);

        Assert.False(result.IsSuccess);
    }

    // ── Money in / out ──────────────────────────────────────────────────────

    [Fact]
    public void Refunds_NetAgainstMoneyOut()
    {
        var report = Build(
            new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month),
            [Income(1, 1000m, D(10, 1)), Expense(2, -300m, D(10, 3)), Expense(3, 50m, D(10, 4))]);

        Assert.Equal(new CashFlowTotals(1000m, 250m), report.Totals);
        Assert.Equal(750m, report.Totals.Net);
        Assert.Equal(250m, Assert.Single(report.Periods).MoneyOut);
    }

    [Fact]
    public void MoneyOut_CanGoNegativeInARefundOnlyPeriod()
    {
        var report = Build(new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month), [Expense(1, 50m, D(10, 4))]);

        Assert.Equal(-50m, report.Totals.MoneyOut);
    }

    [Fact]
    public void TransfersAndAdjustments_AreExcluded()
    {
        var report = Build(
            new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month),
            [Txn(1, "Transfer", -500m, D(10, 2)), Txn(2, "Transfer", 500m, D(10, 2)), Txn(3, "Adjustment", -9m, D(10, 2))]);

        Assert.Equal(new CashFlowTotals(0m, 0m), report.Totals);
    }

    [Fact]
    public void Pending_IsExcludedFromFiguresAndReportedSeparately()
    {
        var report = Build(
            new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month),
            [
                Expense(1, -30m, D(10, 2)),
                Txn(2, "Expense", -20m, D(10, 19), pending: true),
                Txn(3, "Income", 100m, D(10, 19), payee: "Acme", pending: true)
            ]);

        Assert.Equal(new CashFlowTotals(0m, 30m), report.Totals);
        Assert.Equal(30m, Assert.Single(report.Periods).MoneyOut);
        Assert.Equal(new CashFlowTotals(100m, 20m), report.Pending);
        Assert.Empty(report.IncomeSources);
    }

    [Fact]
    public void RowsOutsideTheWindow_DoNotCount_AndToCoversItsWholeDay()
    {
        var report = Build(
            new CashFlowQuery(D(10, 5), D(10, 10), CashFlowGrouping.Month),
            [
                Expense(1, -1m, D(10, 4)),
                Expense(2, -2m, D(10, 5)),
                Expense(3, -4m, D(10, 10).AddHours(23)),
                Expense(4, -8m, D(10, 11))
            ]);

        Assert.Equal(6m, report.Totals.MoneyOut);
    }

    [Fact]
    public void PreviousTotals_CoverTheCompareWindow()
    {
        var report = Build(
            new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month, CompareFrom: D(9, 1), CompareTo: D(9, 30)),
            [Income(1, 900m, D(9, 15)), Expense(2, -100m, D(9, 30)), Expense(3, -7m, D(8, 31)), Income(4, 1000m, D(10, 1))]);

        Assert.Equal(new CashFlowTotals(900m, 100m), report.PreviousTotals);
        Assert.Equal(new CashFlowTotals(1000m, 0m), report.Totals);
    }

    [Fact]
    public void PreviousTotals_AreNullWhenNotRequested()
    {
        var report = Build(new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month), [Income(1, 900m, D(9, 15))]);

        Assert.Null(report.PreviousTotals);
    }

    // ── Breakdown ───────────────────────────────────────────────────────────

    [Fact]
    public void IncomeSources_NormalisePayeeAndRankLargestFirst()
    {
        var report = Build(
            new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month),
            [
                Income(1, 2000m, D(10, 1), "ACME PAYROLL 1234"),
                Income(2, 2000m, D(10, 15), "Acme  Payroll 5678"),
                Income(3, 40m, D(10, 3), "  Zelle   from Bob 0301 "),
                Income(4, 10m, D(10, 4), null),
                Income(5, 5m, D(10, 5), "12345"),
                Expense(6, -999m, D(10, 6))
            ]);

        Assert.Equal(
            [new IncomeSource("ACME PAYROLL", 4000m), new IncomeSource("Zelle from Bob", 40m), new IncomeSource("Other", 15m)],
            report.IncomeSources);
    }

    [Fact]
    public void SpendByCategory_IsNetOfRefunds()
    {
        var report = Build(
            new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month),
            [Expense(1, -80m, D(10, 2), categoryId: 1), Expense(2, 30m, D(10, 3), categoryId: 1), Expense(3, -20m, D(10, 3)), Income(4, 500m, D(10, 1))]);

        Assert.Equal(2, report.SpendByCategory.Count);
        Assert.Equal(50m, report.SpendByCategory.Single(s => s.CategoryId == 1).Amount);
        Assert.Equal(20m, report.SpendByCategory.Single(s => s.CategoryId is null).Amount);
    }

    // ── Balances ────────────────────────────────────────────────────────────

    private static readonly PlaidAccount[] Accounts =
    [
        Account("chk", "depository", 1000m),
        Account("sav", "depository", 500m),
        Account("card", "credit", 300m),
        Account("brokerage", "investment", 9999m),
        Account("new-chk", "depository", null)
    ];

    [Fact]
    public void CurrentBalance_IsCashMinusCardDebt()
    {
        var report = Build(new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month), [], Accounts);

        Assert.Equal(1200m, report.CurrentBalance);
    }

    [Fact]
    public void CurrentBalance_IsNullWithNoSyncedCashOrCreditAccount()
    {
        var report = Build(
            new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month), [],
            [Account("brokerage", "investment", 9999m), Account("new-chk", "depository", null)]);

        Assert.Null(report.CurrentBalance);
        Assert.All(report.Periods, p => Assert.Null(p.Balance));
    }

    [Theory]
    [InlineData(5, null)]
    [InlineData(null, "rent")]
    public void CurrentBalance_IsNullWithACategoryOrSearchFilter(int? categoryId, string? search)
    {
        var report = Build(new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month, CategoryId: categoryId, Search: search), [], Accounts);

        Assert.Null(report.CurrentBalance);
        Assert.Null(Assert.Single(report.Periods).Balance);
    }

    [Theory]
    [InlineData("card", -300.0)]
    [InlineData("sav", 500.0)]
    [InlineData("brokerage", null)]
    public void CurrentBalance_WithAnAccountFilter_IsThatAccountOnly(string plaidAccountId, double? expected)
    {
        var report = Build(new CashFlowQuery(D(10, 1), Today, CashFlowGrouping.Month, PlaidAccountId: plaidAccountId), [], Accounts);

        Assert.Equal((decimal?)expected, report.CurrentBalance);
    }

    [Fact]
    public void PeriodBalance_IsReconstructedBackwardsFromCurrentBalance()
    {
        // Current: 1000 checking - 300 card = 700. Each bucket's balance undoes every posted row in those two
        // accounts dated after the bucket ends; the current month ends today.
        var report = Build(
            new CashFlowQuery(D(8, 1), Today, CashFlowGrouping.Month),
            [
                Income(1, 2000m, D(9, 10)),
                Expense(2, -30m, D(9, 20), plaidAccountId: "card"),
                Expense(3, -100m, D(10, 5)),
                Txn(4, "Transfer", -200m, D(10, 8)),                    // to brokerage: still moves checking
                Expense(5, -50m, D(10, 12), plaidAccountId: "card"),
                Txn(6, "Expense", -999m, D(10, 19), pending: true),     // pending: not in the synced balance
                Expense(7, -77m, D(10, 3), plaidAccountId: "brokerage"), // not a balance account
                Expense(8, -66m, D(10, 3), plaidAccountId: null)         // manual row
            ],
            [Account("chk", "depository", 1000m), Account("card", "credit", 300m), Account("brokerage", "investment", 5m)]);

        Assert.Equal(700m, report.CurrentBalance);
        Assert.Equal([-920m, 1050m, 700m], report.Periods.Select(p => p.Balance));
    }

    [Fact]
    public void PeriodBalance_OfABucketCutShortByTo_IsTheBalanceOnTo()
    {
        // Jun 10–19 by month: the June bar's balance is Jun 19's, so the Jun 25 spend is undone too.
        var report = Build(
            new CashFlowQuery(D(6, 10), D(6, 19), CashFlowGrouping.Month),
            [Expense(1, -40m, D(6, 15)), Expense(2, -100m, D(6, 25)), Expense(3, -7m, D(8, 1))],
            [Account("chk", "depository", 1000m)]);

        Assert.Equal(1107m, Assert.Single(report.Periods).Balance);
    }

    [Fact]
    public void PeriodBalance_UsesRowsDatedAfterTheWindow()
    {
        var report = Build(
            new CashFlowQuery(D(9, 1), D(9, 30), CashFlowGrouping.Month),
            [Expense(1, -100m, D(10, 5))],
            [Account("chk", "depository", 1000m)]);

        Assert.Equal(1100m, Assert.Single(report.Periods).Balance);
        Assert.Equal(0m, report.Totals.MoneyOut);
    }

    [Fact]
    public void PeriodBalance_CardFilter_RebuildsTheCardDebtAlone()
    {
        // $300 owed now. Before the Oct 5 $200 payment and Oct 12 $50 purchase it was $450 owed.
        var report = Build(
            new CashFlowQuery(D(8, 1), Today, CashFlowGrouping.Month, PlaidAccountId: "card"),
            [
                Txn(1, "Transfer", 200m, D(10, 5), plaidAccountId: "card"),
                Expense(2, -50m, D(10, 12), plaidAccountId: "card")
            ],
            [Account("chk", "depository", 1000m), Account("card", "credit", 300m)]);

        Assert.Equal(-300m, report.CurrentBalance);
        Assert.Equal([-450m, -450m, -300m], report.Periods.Select(p => p.Balance));
    }

    [Fact]
    public void PeriodBalance_NonBalanceAccountFilter_LeavesEveryPeriodNull()
    {
        var report = Build(
            new CashFlowQuery(D(8, 1), Today, CashFlowGrouping.Month, PlaidAccountId: "brokerage"),
            [Expense(1, -77m, D(9, 3), plaidAccountId: "brokerage")],
            Accounts);

        Assert.Null(report.CurrentBalance);
        Assert.All(report.Periods, p => Assert.Null(p.Balance));
        Assert.Equal(77m, report.Totals.MoneyOut);
    }
}
