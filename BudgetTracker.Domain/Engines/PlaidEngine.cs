using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;

namespace BudgetTracker.Domain.Engines;

/// <summary>
/// Pure business logic for translating Plaid responses into BudgetTracker domain objects.
/// Handles the sign-convention flip (Plaid positive = outflow → BudgetTracker negative amount), the
/// Income/Expense/Transfer classification, and which BudgetTracker account a Plaid-sourced transaction belongs to.
/// </summary>
public class PlaidEngine : IPlaidEngine
{
    private const string Income = "Income";
    private const string Expense = "Expense";
    private const string Transfer = "Transfer";
    private const string Adjustment = "Adjustment";
    private const string PlaidIncomeCategory = "INCOME";
    private const int TransferWindowDays = 5;

    /// <summary>Read-only history before a windowed re-pair's apply range; see <see cref="GetTransferPairingWindow"/>.</summary>
    private const int PairingContextDays = 8 * TransferWindowDays;

    // Plaid files card payments and transfers to unlinked accounts (Zelle rent, a car loan) under these too, so the
    // category only nominates candidates; a matching leg on another linked account is what makes it a transfer.
    private static readonly HashSet<string> TransferCategorySet = ["TRANSFER_IN", "TRANSFER_OUT", "LOAN_PAYMENTS"];

    /// <inheritdoc />
    public Transaction MapToBudgetTrackerTransaction(PlaidTransactionDto dto, int accountId, int? categoryId = null, string? categoryType = null)
    {
        // Plaid convention: positive = money out, negative = money in.
        // BudgetTracker convention (BUD-18): negative = outflow, positive = inflow. The sign no longer decides the
        // type: a refund is a positive Expense that nets against spending.
        return new Transaction
        {
            AccountId = accountId,
            TransactionType = ClassifyTransactionType(dto.PersonalFinanceCategoryPrimary, categoryType),
            Amount = -dto.Amount,
            OccurredAt = dto.Date,
            Payee = PayeeOf(dto),
            PlaidTransactionId = dto.TransactionId,
            PlaidAccountId = dto.AccountId,
            PlaidCategoryPrimary = dto.PersonalFinanceCategoryPrimary,
            CategoryId = categoryId,
            IsImported = true,
            IsPending = dto.Pending
        };
    }

    private static string PayeeOf(PlaidTransactionDto dto) =>
        !string.IsNullOrWhiteSpace(dto.MerchantName) ? dto.MerchantName : dto.Name;

    /// <inheritdoc />
    public string ClassifyTransactionType(string? plaidCategoryPrimary, string? categoryType, string? currentType = null) =>
        // Transfer belongs to ReconcileOwnAccountTransfers, which re-pairs every sync and reverts a lost pair by calling this
        // without currentType. A Transfer-type category is the manual half: money that moved to an account that is not
        // linked (Webull, a card paid off-platform) becomes an Adjustment, which no spending or cash-flow figure counts.
        currentType == Transfer ? currentType
        : categoryType == Transfer ? Adjustment
        : categoryType is Income or Expense ? categoryType
        : plaidCategoryPrimary == PlaidIncomeCategory ? Income
        : Expense;

    /// <inheritdoc />
    public IReadOnlyCollection<string> TransferCategories => TransferCategorySet;

    /// <inheritdoc />
    public IReadOnlyList<TransferUpdate> ReconcileOwnAccountTransfers(
        IEnumerable<Transaction> candidates,
        IEnumerable<PlaidAccount> linkedAccounts,
        IEnumerable<Category> userCategories,
        TransferPairingWindow? window = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(linkedAccounts);
        ArgumentNullException.ThrowIfNull(userCategories);

        // Only legs on cash accounts pair, so transfers net out exactly where the balance chart counts money.
        var cashAccountIds = linkedAccounts
            .Where(a => PlaidAccountTypes.IsCash(a.AccountType))
            .Select(a => a.PlaidAccountId)
            .ToHashSet();
        var categoryTypeById = userCategories.ToDictionary(c => c.Id, c => c.CategoryType);

        var pool = candidates
            .Where(t => !t.IsPending && t.TransactionType != Adjustment &&
                        t.PlaidCategoryPrimary is not null && TransferCategorySet.Contains(t.PlaidCategoryPrimary))
            .ToList();

        bool InApplyRange(Transaction t) =>
            window is null || (t.OccurredAt.Date >= window.ApplyFrom && t.OccurredAt.Date <= window.ApplyTo);

        var counterpartById = PairLegs(pool.Where(t => t.PlaidAccountId is not null && cashAccountIds.Contains(t.PlaidAccountId)));

        // A context row only changes when it is the partner an apply-range row now pairs with, so a pair never ends up
        // half-written.
        bool MayChange(Transaction t) =>
            InApplyRange(t) || (counterpartById.TryGetValue(t.Id, out var partner) && InApplyRange(partner));

        var updates = new List<TransferUpdate>();
        foreach (var row in pool.Where(MayChange).OrderBy(t => t.Id))
        {
            TransferUpdate? update = counterpartById.TryGetValue(row.Id, out var counterpart)
                ? new(row.Id, Transfer, counterpart.AccountId)
                : row.TransactionType == Transfer
                    ? new(row.Id, ClassifyTransactionType(row.PlaidCategoryPrimary, CategoryTypeOf(row, categoryTypeById)), null)
                    : null;

            if (update is not null && (update.TransactionType != row.TransactionType || update.TransferAccountId != row.TransferAccountId))
                updates.Add(update);
        }

        return updates;
    }

    private static string? CategoryTypeOf(Transaction row, Dictionary<int, string> categoryTypeById) =>
        row.CategoryId is int id ? categoryTypeById.GetValueOrDefault(id) : null;

    /// <summary>
    /// Pair legs of equal amount by date. Outflows and inflows are each walked oldest first (id breaks ties) and every
    /// outflow takes the oldest unused inflow inside its window. With one window width for all legs that is a
    /// maximum matching, unlike closest-first, which can strand legs.
    /// </summary>
    /// <returns>Each paired row's counterpart row, by row id.</returns>
    private static Dictionary<int, Transaction> PairLegs(IEnumerable<Transaction> legs)
    {
        var counterpartById = new Dictionary<int, Transaction>();
        foreach (var sameAmount in legs.GroupBy(t => Math.Abs(t.Amount)))
        {
            var outflows = sameAmount.Where(t => t.Amount < 0).OrderBy(t => t.OccurredAt.Date).ThenBy(t => t.Id).ToList();
            var inflows = sameAmount.Where(t => t.Amount > 0).OrderBy(t => t.OccurredAt.Date).ThenBy(t => t.Id).ToList();
            var used = new bool[inflows.Count];
            var first = 0;

            foreach (var outflow in outflows)
            {
                var day = outflow.OccurredAt.Date;

                // Outflows only get later, so an inflow too early for this one is too early for every one after it.
                while (first < inflows.Count && (used[first] || inflows[first].OccurredAt.Date < day.AddDays(-TransferWindowDays)))
                    first++;

                // ponytail: skipping a same-account inflow keeps the walk simple but can cost a pair in a contrived
                // reversal-plus-transfer case; a full bipartite matching is the upgrade if that ever shows up.
                for (var i = first; i < inflows.Count && inflows[i].OccurredAt.Date <= day.AddDays(TransferWindowDays); i++)
                {
                    if (used[i] || inflows[i].PlaidAccountId == outflow.PlaidAccountId)
                        continue;

                    used[i] = true;
                    counterpartById[outflow.Id] = inflows[i];
                    counterpartById[inflows[i].Id] = outflow;
                    break;
                }
            }
        }

        return counterpartById;
    }

    /// <inheritdoc />
    public TransferPairingWindow? GetTransferPairingWindow(IEnumerable<DateTime> touchedDates)
    {
        ArgumentNullException.ThrowIfNull(touchedDates);

        var days = touchedDates.Select(d => d.Date).ToList();
        if (days.Count == 0)
            return null;

        // Apply: every row that could pair with a touched row. Fetch, after the apply range: legs that can take an
        // apply-range inflow are dated at most one tolerance later, and pairing walks oldest first, so nothing later can
        // change an apply-range result. Fetch, before it: pairing is one greedy walk over all history, so an earlier leg
        // can shift which partner a later one gets; a long run of read-only context lets that settle before the apply
        // range (randomised ledgers with dense same-amount transfers matched a from-scratch run every time at this depth).
        var first = days.Min();
        var last = days.Max();
        var applyFrom = first.AddDays(-TransferWindowDays);
        var applyTo = last.AddDays(TransferWindowDays);
        return new TransferPairingWindow(
            applyFrom,
            applyTo,
            applyFrom.AddDays(-PairingContextDays),
            applyTo.AddDays(TransferWindowDays));
    }

    /// <inheritdoc />
    public string ResolvePayee(PlaidTransactionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return PayeeOf(dto);
    }

    /// <inheritdoc />
    public bool IsImportable(PlaidTransactionDto dto) => dto.Amount != 0;

    /// <inheritdoc />
    public int? ResolveCategoryId(
        PlaidTransactionDto dto, IEnumerable<Category> userCategories, IReadOnlyDictionary<string, int>? categoryByPayee = null)
    {
        // ponytail: exact payee match, so "Uber 072515 SF**POOL**" misses "Uber"; normalise payees or add an explicit
        // rules table if misses pile up.
        if (categoryByPayee is not null && categoryByPayee.TryGetValue(PayeeOf(dto), out var picked))
            return picked;

        if (string.IsNullOrWhiteSpace(dto.PersonalFinanceCategoryPrimary))
            return null;

        var match = userCategories.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.PlaidCategoryPrimary) &&
            string.Equals(c.PlaidCategoryPrimary, dto.PersonalFinanceCategoryPrimary, StringComparison.OrdinalIgnoreCase));

        return match?.Id;
    }

    /// <inheritdoc />
    public int? ResolveBudgetTrackerAccountId(PlaidAccountDto plaidAccount, IEnumerable<Account> userAccounts)
    {
        var displayName = BuildDisplayName(plaidAccount, institutionName: null);
        var match = userAccounts.FirstOrDefault(a =>
            string.Equals(a.Name, displayName, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
            return match.Id;

        // Try fallback: any account name containing both the Plaid name and (if present) the mask suffix.
        var maskSuffix = string.IsNullOrEmpty(plaidAccount.Mask) ? null : $"••{plaidAccount.Mask}";
        var fallback = userAccounts.FirstOrDefault(a =>
            a.Name.Contains(plaidAccount.Name, StringComparison.OrdinalIgnoreCase) &&
            (maskSuffix is null || a.Name.Contains(maskSuffix, StringComparison.OrdinalIgnoreCase)));

        return fallback?.Id;
    }

    /// <inheritdoc />
    public bool IsAlreadyLinked(string institutionId, IReadOnlyList<PlaidAccountDto> incomingAccounts, IEnumerable<PlaidItem> activeItems)
    {
        ArgumentNullException.ThrowIfNull(incomingAccounts);
        ArgumentNullException.ThrowIfNull(activeItems);

        return activeItems.Where(item => item.IsActive).Any(item => item.Accounts.Any(existing => incomingAccounts.Any(incoming =>
            existing.PlaidAccountId == incoming.AccountId ||
            (item.InstitutionId == institutionId && IsSameAccountByMaskAndName(existing, incoming)))));
    }

    /// <summary>A null mask is treated as unknown, never as a match, so generic names alone cannot block a link.</summary>
    private static bool IsSameAccountByMaskAndName(PlaidAccount existing, PlaidAccountDto incoming) =>
        existing.Mask is not null &&
        existing.Mask == incoming.Mask &&
        string.Equals(existing.Name, incoming.Name, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Account BuildBudgetTrackerAccount(PlaidAccountDto plaidAccount, string institutionName, int userId)
    {
        return new Account
        {
            UserId = userId,
            Name = BuildDisplayName(plaidAccount, institutionName),
            AccountType = plaidAccount.Type
        };
    }

    private static string BuildDisplayName(PlaidAccountDto plaidAccount, string? institutionName)
    {
        var prefix = string.IsNullOrWhiteSpace(institutionName) ? string.Empty : $"{institutionName} - ";
        var maskSuffix = string.IsNullOrEmpty(plaidAccount.Mask) ? string.Empty : $" (••{plaidAccount.Mask})";
        return $"{prefix}{plaidAccount.Name}{maskSuffix}";
    }

    public Result<IReadOnlyList<PlaidItem>> SelectItemsDueForSync(IEnumerable<PlaidItem> items, DateTime now, int? staleAfterHours)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (staleAfterHours is < 0)
            return Result<IReadOnlyList<PlaidItem>>.Failure("staleAfterHours cannot be negative");

        if (staleAfterHours is not int hours)
            return Result<IReadOnlyList<PlaidItem>>.Success(items.ToList());

        var cutoff = now.AddHours(-hours);
        return Result<IReadOnlyList<PlaidItem>>.Success(
            items.Where(i => i.LastSyncedAt is null || i.LastSyncedAt < cutoff).ToList());
    }
}
