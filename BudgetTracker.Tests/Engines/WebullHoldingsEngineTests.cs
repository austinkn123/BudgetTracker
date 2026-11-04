using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Webull;

namespace BudgetTracker.Tests.Engines;

/// <summary>
/// Tests for <see cref="WebullHoldingsEngine"/>: per-row valuation for EQUITY and CRYPTO only, null (never 0) for missing
/// inputs or other instruments, per-account % of holdings excluding cash, rounding, sorting, and the view header copy.
/// </summary>
public class WebullHoldingsEngineTests
{
    private static readonly DateTime AsOf = new(2026, 10, 23, 20, 35, 0, DateTimeKind.Utc);
    private static readonly WebullConnectionView Connected =
        new(WebullState.Connected, [], AsOf, false, WebullErrorKind.Unreachable, null, false);

    private readonly WebullHoldingsEngine _sut = new();

    private static WebullPosition Position(
        string symbol = "AAPL",
        string type = "EQUITY",
        decimal? quantity = 10m,
        decimal? lastPrice = 150m,
        decimal? costPrice = 120m,
        decimal? unrealized = 300m) =>
        new(symbol, type, quantity, lastPrice, costPrice, unrealized, "USD");

    private static WebullAccountPortfolio Account(string id = "ACC1", params WebullPosition[] positions) =>
        new(new WebullBalance(id, "Individual Cash", "CASH", 5000m, 1200m, "USD"), 3800m, -12.5m, 450m, positions);

    private WebullHolding Single(WebullPosition position) =>
        Assert.Single(Assert.Single(_sut.BuildView(Connected, [Account(positions: position)]).Accounts).Positions);

    // ── View header ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildView_CopiesStateAsOfStaleAndLastErrorFromConnection()
    {
        var stale = Connected with { State = WebullState.NeedsVerification, IsStale = true, LastError = WebullErrorKind.TokenRejected };

        var view = _sut.BuildView(stale, [Account(positions: Position())]);

        Assert.Equal(WebullState.NeedsVerification, view.State);
        Assert.Equal(AsOf, view.AsOf);
        Assert.True(view.IsStale);
        Assert.Equal(WebullErrorKind.TokenRejected, view.LastError);
        Assert.Single(view.Accounts); // every state but NotSetUp shows what is saved
    }

    [Fact]
    public void BuildView_NotSetUp_ShowsNoAccountsEvenIfSaved()
    {
        var notSetUp = new WebullConnectionView(WebullState.NotSetUp, [], null, false, null, null, false);

        Assert.Empty(_sut.BuildView(notSetUp, [Account(positions: Position())]).Accounts);
    }

    [Fact]
    public void BuildView_AccountHeader_ComesFromTheBalance_AndAccountsKeepTheirOrder()
    {
        var view = _sut.BuildView(Connected, [Account("B"), Account("A")]);

        Assert.Equal(["B", "A"], view.Accounts.Select(a => a.AccountId));
        var header = view.Accounts[0];
        Assert.Equal(("Individual Cash", "CASH", "USD"), (header.AccountLabel, header.AccountType, header.Currency));
        Assert.Equal(5000m, header.TotalValue);
        Assert.Equal(1200m, header.Cash);
        Assert.Equal(-12.5m, header.DayProfitLoss);
        Assert.Equal(450m, header.OpenProfitLoss);
        Assert.Empty(header.Positions);
    }

    // ── Row valuation ───────────────────────────────────────────────────────

    [Fact]
    public void Equity_ComputesEveryValue_WithCostPriceReadPerShare()
    {
        var row = Single(Position(quantity: 10m, lastPrice: 150m, costPrice: 120m, unrealized: 300m));

        Assert.Equal(new WebullHolding("AAPL", "EQUITY", 10m, 120m, 1200m, 1500m, 300m, 25.00m, 100.00m), row);
    }

    [Fact]
    public void Crypto_FractionalQuantity_MoneyIsNotRounded()
    {
        var row = Single(Position("BTCUSD", "CRYPTO", quantity: 0.5m, lastPrice: 60000.123m, costPrice: 50000m, unrealized: 5000.0615m));

        Assert.Equal(30000.0615m, row.MarketValue);
        Assert.Equal(25000m, row.TotalCost);
        Assert.Equal(20.00m, row.OpenProfitLossPercent);
    }

    [Theory]
    [InlineData("OPTION")]
    [InlineData("FUTURES")]
    [InlineData("EVENT")]
    [InlineData("equity")] // instrument types are matched exactly as Webull sends them
    public void OtherInstrumentTypes_ArePlainRowsWithNoComputedValues(string type)
    {
        var row = Single(Position("SPY260116C00600000", type, quantity: 2m));

        Assert.Equal(new WebullHolding("SPY260116C00600000", type, 2m, null, null, null, null, null, null), row);
    }

    [Fact]
    public void MissingQuantity_NullsEveryValueThatNeedsIt()
    {
        var row = Single(Position(quantity: null));

        Assert.Null(row.MarketValue);
        Assert.Null(row.TotalCost);
        Assert.Null(row.OpenProfitLossPercent);
        Assert.Null(row.PercentOfHoldings);
        Assert.Equal(120m, row.AverageCost);
        Assert.Equal(300m, row.OpenProfitLoss);
    }

    [Fact]
    public void MissingLastPrice_NullsMarketValueAndShareOnly()
    {
        var row = Single(Position(lastPrice: null));

        Assert.Null(row.MarketValue);
        Assert.Null(row.PercentOfHoldings);
        Assert.Equal(1200m, row.TotalCost);
        Assert.Equal(25.00m, row.OpenProfitLossPercent);
    }

    [Fact]
    public void MissingCostPrice_NullsCostAndOpenPercent()
    {
        var row = Single(Position(costPrice: null));

        Assert.Null(row.AverageCost);
        Assert.Null(row.TotalCost);
        Assert.Null(row.OpenProfitLossPercent);
        Assert.Equal(1500m, row.MarketValue);
    }

    [Fact]
    public void MissingUnrealized_NullsOpenValues()
    {
        var row = Single(Position(unrealized: null));

        Assert.Null(row.OpenProfitLoss);
        Assert.Null(row.OpenProfitLossPercent);
        Assert.Equal(1200m, row.TotalCost);
    }

    [Fact]
    public void ZeroTotalCost_OpenPercentIsNull()
    {
        var row = Single(Position(costPrice: 0m, unrealized: 1500m));

        Assert.Equal(0m, row.TotalCost);
        Assert.Null(row.OpenProfitLossPercent);
    }

    [Theory]
    [InlineData(123.45, 12.35)]   // 12.345 rounds away from zero
    [InlineData(-123.45, -12.35)]
    [InlineData(0.04, 0.00)]
    public void OpenPercent_RoundsToTwoDecimalsAwayFromZero(decimal unrealized, decimal expected)
    {
        var row = Single(Position(quantity: 10m, costPrice: 100m, unrealized: unrealized));

        Assert.Equal(expected, row.OpenProfitLossPercent);
    }

    // ── % of holdings ───────────────────────────────────────────────────────

    [Fact]
    public void PercentOfHoldings_IsOverPositionsOnly_PerAccount_AndSumsToAHundred()
    {
        var view = _sut.BuildView(Connected,
        [
            Account("A", Position("X", lastPrice: 10m), Position("Y", lastPrice: 10m), Position("Z", lastPrice: 10m)),
            Account("B", Position("Q", quantity: 1m, lastPrice: 30m), Position("R", quantity: 1m, lastPrice: 10m))
        ]);

        // Cash (1200 per account) is not part of the denominator.
        var a = view.Accounts[0].Positions;
        Assert.All(a, r => Assert.Equal(33.33m, r.PercentOfHoldings));
        Assert.InRange(a.Sum(r => r.PercentOfHoldings!.Value), 99.9m, 100.1m);
        Assert.Equal([75.00m, 25.00m], view.Accounts[1].Positions.Select(r => r.PercentOfHoldings));
    }

    [Fact]
    public void PercentOfHoldings_ExcludesRowsWithoutMarketValue()
    {
        var view = _sut.BuildView(Connected,
            [Account(positions: [Position("A", lastPrice: 10m), Position("B", lastPrice: null), Position("OPT", "OPTION")])]);

        var rows = view.Accounts[0].Positions;
        Assert.Equal(100.00m, rows.Single(r => r.Symbol == "A").PercentOfHoldings);
        Assert.Null(rows.Single(r => r.Symbol == "B").PercentOfHoldings);
        Assert.Null(rows.Single(r => r.Symbol == "OPT").PercentOfHoldings);
    }

    [Fact]
    public void PercentOfHoldings_AccountMarketValueZero_IsNull()
    {
        var row = Single(Position(lastPrice: 0m));

        Assert.Equal(0m, row.MarketValue);
        Assert.Null(row.PercentOfHoldings);
    }

    // ── Sorting ─────────────────────────────────────────────────────────────

    [Fact]
    public void Rows_SortByMarketValueDescending_NullsLast_ThenSymbol()
    {
        var view = _sut.BuildView(Connected,
        [
            Account(positions:
            [
                Position("SMALL", lastPrice: 1m),
                Position("NOPRICE", lastPrice: null),
                Position("BIG", lastPrice: 100m),
                Position("OPT", "OPTION"),
                Position("TIE_B", lastPrice: 50m),
                Position("TIE_A", lastPrice: 50m)
            ])
        ]);

        Assert.Equal(["BIG", "TIE_A", "TIE_B", "SMALL", "NOPRICE", "OPT"], view.Accounts[0].Positions.Select(r => r.Symbol));
    }

    [Fact]
    public void BuildView_AccountHeader_NullTotals_StayNull()
    {
        var account = new WebullAccountPortfolio(new WebullBalance("ACC1", null, "MARGIN", 5000m, null, "USD"), null, null, null, []);

        var header = Assert.Single(_sut.BuildView(Connected, [account]).Accounts);

        Assert.Null(header.AccountLabel);
        Assert.Null(header.Cash);
        Assert.Null(header.DayProfitLoss);
        Assert.Null(header.OpenProfitLoss);
        Assert.Equal(5000m, header.TotalValue);
    }

    // ── Account open return % ───────────────────────────────────────────────

    private static WebullAccountPortfolio AccountWithOpen(decimal? openProfitLoss, params WebullPosition[] positions) =>
        new(new WebullBalance("ACC1", "Individual Margin", "MARGIN", 40000m, 1000m, "USD"), 39000m, 12m, openProfitLoss, positions);

    private decimal? AccountOpenPercent(WebullAccountPortfolio account) =>
        Assert.Single(_sut.BuildView(Connected, [account]).Accounts).OpenProfitLossPercent;

    [Fact]
    public void AccountOpenPercent_IsOpenProfitLossOverSummedPositionCost()
    {
        // Production-like: costs 10,428.80 + 10,274.22 + 9,837.30 = 30,540.32; 8,465.26 / 30,540.32 = 27.718 %.
        var account = AccountWithOpen(8465.26m,
            Position("QQQ", quantity: 20m, lastPrice: 600m, costPrice: 521.44m),
            Position("SPY", quantity: 15m, lastPrice: 700m, costPrice: 684.948m),
            Position("VTI", quantity: 30m, lastPrice: 340m, costPrice: 327.91m));

        Assert.Equal(27.72m, AccountOpenPercent(account));
    }

    [Theory]
    [InlineData(123.45, 12.35)]   // 12.345 rounds away from zero
    [InlineData(-123.45, -12.35)]
    public void AccountOpenPercent_RoundsToTwoDecimalsAwayFromZero(decimal openProfitLoss, decimal expected)
    {
        var account = AccountWithOpen(openProfitLoss, Position(quantity: 10m, costPrice: 100m));

        Assert.Equal(expected, AccountOpenPercent(account));
    }

    [Fact]
    public void AccountOpenPercent_NullOpenProfitLoss_IsNull() =>
        Assert.Null(AccountOpenPercent(AccountWithOpen(null, Position())));

    [Fact]
    public void AccountOpenPercent_NoPositions_IsNull() =>
        Assert.Null(AccountOpenPercent(AccountWithOpen(100m)));

    [Theory]
    [InlineData("OPTION", 2.0, 120.0)]  // non-valued type: no TotalCost
    [InlineData("EQUITY", 2.0, null)]   // missing cost_price
    [InlineData("EQUITY", null, 120.0)] // missing quantity
    public void AccountOpenPercent_AnyPositionWithoutTotalCost_IsNull(string type, double? quantity, double? costPrice)
    {
        var partial = Position("X", type, quantity: (decimal?)quantity, costPrice: (decimal?)costPrice);

        Assert.Null(AccountOpenPercent(AccountWithOpen(100m, Position("AAPL"), partial)));
    }

    [Fact]
    public void AccountOpenPercent_ZeroSummedCost_IsNull() =>
        Assert.Null(AccountOpenPercent(AccountWithOpen(100m, Position(costPrice: 0m), Position("B", costPrice: 0m))));
}
