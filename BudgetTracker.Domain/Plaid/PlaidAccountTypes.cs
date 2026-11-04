namespace BudgetTracker.Domain.Plaid;

/// <summary>Plaid account types that hold the user's cash: checking/savings (depository) and cards (credit).</summary>
public static class PlaidAccountTypes
{
    public const string Depository = "depository";
    public const string Credit = "credit";

    /// <summary>
    /// True for depository and credit accounts: the ones the cash-flow balance counts and own-account transfers pair
    /// between. Loans and investments are outside it, so money moved to them is money out.
    /// </summary>
    public static bool IsCash(string? accountType) => IsCredit(accountType) ||
        string.Equals(accountType, Depository, StringComparison.OrdinalIgnoreCase);

    /// <summary>True for a card, whose balance is money owed.</summary>
    public static bool IsCredit(string? accountType) =>
        string.Equals(accountType, Credit, StringComparison.OrdinalIgnoreCase);
}
