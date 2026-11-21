using System.Globalization;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Engines;

/// <inheritdoc />
public class TransactionExportEngine : ITransactionExportEngine
{
    private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];
    private static readonly char[] NeedsQuotes = [',', '"', '\n', '\r'];

    /// <inheritdoc />
    public string HeaderLine => "Date,Payee,Amount,Type,Category,Account,Pending,Notes,Plaid category";

    /// <inheritdoc />
    public string FormatRow(TransactionExportRow row) => string.Join(',',
        row.OccurredAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Text(row.Payee),
        // Numeric on purpose: a leading minus here is a sign, not a formula.
        Math.Round(row.Amount, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture),
        Text(row.TransactionType),
        Text(row.CategoryName),
        Text(row.AccountName),
        row.IsPending ? "true" : "false",
        Text(row.Notes),
        Text(row.PlaidCategoryPrimary));

    /// <inheritdoc />
    public string FileName(DateTime? from, DateTime? to)
    {
        static string Day(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var range = (from, to) switch
        {
            ({ } f, { } t) => $"{Day(f)}-to-{Day(t)}",
            (null, { } t) => $"to-{Day(t)}",
            ({ } f, null) => $"{Day(f)}-onward",
            _ => "all",
        };
        return $"budgettracker-transactions-{range}.csv";
    }

    private static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (FormulaStarts.Contains(value[0]))
            value = "'" + value;

        return value.IndexOfAny(NeedsQuotes) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
