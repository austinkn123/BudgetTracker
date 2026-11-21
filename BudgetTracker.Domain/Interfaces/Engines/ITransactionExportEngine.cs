using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

/// <summary>Formats transactions as RFC 4180 CSV (BUD-25). Pure: no I/O, culture-invariant.</summary>
public interface ITransactionExportEngine
{
    /// <summary>The header row, without a line break.</summary>
    string HeaderLine { get; }

    /// <summary>
    /// One row, without a line break. Text cells are quoted when they hold a comma, quote or line break, and a text cell
    /// starting with =, +, -, @, tab or CR gets a leading ' so a spreadsheet won't run it as a formula.
    /// </summary>
    string FormatRow(TransactionExportRow row);

    /// <summary>The download's file name for the exported date range.</summary>
    string FileName(DateTime? from, DateTime? to);
}
