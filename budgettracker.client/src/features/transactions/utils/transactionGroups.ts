import { format, isSameMonth, parseISO } from 'date-fns';
import type { Transaction } from '../../../shared/types/api';

export type TransactionDaySummary = {
  dateKey: string;
  label: string;
  transactions: Transaction[];
  incomeTotal: number;
  outflowTotal: number;
  netTotal: number;
  transactionCount: number;
};

export type TransactionMonthSummary = {
  label: string;
  activeDayCount: number;
  transactionCount: number;
  incomeTotal: number;
  outflowTotal: number;
  netTotal: number;
};

// BUD-18: Transaction.amount is already signed. Display totals (incomeTotal,
// outflowTotal) are expressed as non-negative magnitudes; netTotal carries the sign.
const getOutflowMagnitude = (transaction: Transaction) =>
  transaction.amount < 0 ? -transaction.amount : transaction.amount;

// Direction is the sign, never the type: a refund is an Expense row with a positive
// amount, and an own-account Transfer can go either way. Money in is amount > 0.
export const isInflowTransaction = (transaction: Transaction) => transaction.amount > 0;

// A paired own-account transfer is out of every figure already, so it never waits on a category.
export const needsCategory = (transaction: Transaction) =>
  transaction.categoryId == null && transaction.transactionType !== 'Transfer';

export const toDateKey = (value: Date | string) =>
  format(typeof value === 'string' ? parseISO(value) : value, 'yyyy-MM-dd');

export const buildTransactionDaySummaries = (transactions: Transaction[]) => {
  const summaries = new Map<string, TransactionDaySummary>();

  for (const transaction of transactions) {
    const occurredAt = parseISO(transaction.occurredAt);
    const dateKey = toDateKey(occurredAt);
    const isInflow = isInflowTransaction(transaction);
    const existing = summaries.get(dateKey);

    if (existing) {
      existing.transactions.push(transaction);
      existing.transactionCount += 1;
      if (isInflow) {
        existing.incomeTotal += getOutflowMagnitude(transaction);
      } else {
        existing.outflowTotal += getOutflowMagnitude(transaction);
      }
      existing.netTotal += transaction.amount;
      continue;
    }

    summaries.set(dateKey, {
      dateKey,
      label: format(occurredAt, 'PPPP'),
      transactions: [transaction],
      transactionCount: 1,
      incomeTotal: isInflow ? getOutflowMagnitude(transaction) : 0,
      outflowTotal: isInflow ? 0 : getOutflowMagnitude(transaction),
      netTotal: transaction.amount,
    });
  }

  for (const summary of summaries.values()) {
    summary.transactions = summary.transactions
      .slice()
      .sort((left, right) => right.occurredAt.localeCompare(left.occurredAt));
  }

  return summaries;
};

export const getTransactionDaySummary = (
  daySummaries: Map<string, TransactionDaySummary>,
  selectedDate: Date,
) => daySummaries.get(toDateKey(selectedDate)) ?? null;

export const getTransactionMonthSummary = (
  daySummaries: Map<string, TransactionDaySummary>,
  selectedDate: Date,
): TransactionMonthSummary => {
  let activeDayCount = 0;
  let transactionCount = 0;
  let incomeTotal = 0;
  let outflowTotal = 0;
  let netTotal = 0;

  for (const summary of daySummaries.values()) {
    if (!isSameMonth(parseISO(summary.dateKey), selectedDate)) {
      continue;
    }

    activeDayCount += 1;
    transactionCount += summary.transactionCount;
    incomeTotal += summary.incomeTotal;
    outflowTotal += summary.outflowTotal;
    netTotal += summary.netTotal;
  }

  return {
    label: format(selectedDate, 'MMMM yyyy'),
    activeDayCount,
    transactionCount,
    incomeTotal,
    outflowTotal,
    netTotal,
  };
};
export type PayeeGroup = {
  payee: string;
  ids: number[];
  netTotal: number;
  plaidCategoryPrimary: string | null;
};

/**
 * The uncategorized backlog collapsed to one entry per payee, most rows first, so a payee is filed
 * once instead of row by row. Payees match case-insensitively, as sync does when it learns from
 * these picks.
 */
export const groupUncategorizedByPayee = (transactions: Transaction[]): PayeeGroup[] => {
  const groups = new Map<string, PayeeGroup>();

  for (const transaction of transactions) {
    if (!needsCategory(transaction)) continue;

    const payee = transaction.payee?.trim() || 'No payee';
    const key = payee.toLowerCase();
    const group = groups.get(key) ?? {
      payee,
      ids: [],
      netTotal: 0,
      plaidCategoryPrimary: transaction.plaidCategoryPrimary ?? null,
    };

    group.ids.push(transaction.id);
    group.netTotal += transaction.amount;
    groups.set(key, group);
  }

  return [...groups.values()].sort(
    (a, b) => b.ids.length - a.ids.length || Math.abs(b.netTotal) - Math.abs(a.netTotal),
  );
};
