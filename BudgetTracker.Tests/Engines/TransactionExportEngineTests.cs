using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Tests.Engines;

public class TransactionExportEngineTests
{
    private readonly TransactionExportEngine _engine = new();

    private static TransactionExportRow Row(
        string? payee = "Corner Shop", decimal amount = -12.5m, string? category = "Groceries", string? notes = null,
        bool pending = false, string? plaid = "FOOD_AND_DRINK", string account = "Chase Checking ••1234", string type = "Expense") =>
        new(new DateTime(2026, 11, 3, 0, 0, 0), payee, amount, type, category, account, pending, notes, plaid);

    [Fact]
    public void Should_NameTheColumnsInOrder()
    {
        Assert.Equal("Date,Payee,Amount,Type,Category,Account,Pending,Notes,Plaid category", _engine.HeaderLine);
    }

    [Fact]
    public void Should_FormatAPlainRow()
    {
        Assert.Equal(
            "2026-11-03,Corner Shop,-12.50,Expense,Groceries,Chase Checking ••1234,false,,FOOD_AND_DRINK",
            _engine.FormatRow(Row()));
    }

    [Fact]
    public void Should_LeaveMissingValuesBlank()
    {
        Assert.Equal(
            "2026-11-03,,-12.50,Expense,,Chase Checking ••1234,true,,",
            _engine.FormatRow(Row(payee: null, category: null, pending: true, plaid: null)));
    }

    [Theory]
    [InlineData("Smith, Jones & Co", "\"Smith, Jones & Co\"")]
    [InlineData("The \"Best\" Cafe", "\"The \"\"Best\"\" Cafe\"")]
    [InlineData("Line one\nline two", "\"Line one\nline two\"")]
    [InlineData("Café", "Café")]
    public void Should_QuoteAndEscapePerRfc4180(string payee, string expected)
    {
        Assert.Equal(expected, Field(_engine.FormatRow(Row(payee: payee)), 1));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+1 555", "'+1 555")]
    [InlineData("-refund", "'-refund")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tTAB", "'\tTAB")]
    public void Should_NeutralizeFormulas_InTextCells(string payee, string expected)
    {
        Assert.Equal(expected, Field(_engine.FormatRow(Row(payee: payee)), 1));
    }

    [Fact]
    public void Should_NeutralizeFormulasInNotesAndCategoryToo()
    {
        var line = _engine.FormatRow(Row(category: "=cmd", notes: "@me"));

        Assert.Equal("'=cmd", Field(line, 4));
        Assert.Equal("'@me", Field(line, 7));
    }

    [Theory]
    [InlineData(-1234.5, "-1234.50")]
    [InlineData(2500, "2500.00")]
    [InlineData(0.004, "0.00")]
    public void Should_WriteAmountsAsPlainSignedNumbers(double amount, string expected)
    {
        Assert.Equal(expected, Field(_engine.FormatRow(Row(amount: (decimal)amount)), 2));
    }

    [Fact]
    public void Should_UseInvariantFormatting_Regardless_OfCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("-12.50", Field(_engine.FormatRow(Row()), 2));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("2026-11-01", "2026-11-30", "budgettracker-transactions-2026-11-01-to-2026-11-30.csv")]
    [InlineData(null, "2026-11-30", "budgettracker-transactions-to-2026-11-30.csv")]
    [InlineData("2026-11-01", null, "budgettracker-transactions-2026-11-01-onward.csv")]
    [InlineData(null, null, "budgettracker-transactions-all.csv")]
    public void Should_NameTheFileAfterTheRange(string? from, string? to, string expected)
    {
        Assert.Equal(expected, _engine.FileName(from is null ? null : DateTime.Parse(from), to is null ? null : DateTime.Parse(to)));
    }

    /// <summary>The n-th field of a CSV line, honouring quotes (kept raw, quotes included).</summary>
    private static string Field(string line, int index)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var c in line)
        {
            if (c == '"') quoted = !quoted;
            if (c == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        fields.Add(current.ToString());
        return fields[index];
    }
}
