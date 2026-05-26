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

// BUD-18: Transaction.amount is already signed (Expense/Transfer negative,
// Income positive). Display totals (incomeTotal, outflowTotal) are expressed
// as non-negative magnitudes; netTotal carries the sign.
const getOutflowMagnitude = (transaction: Transaction) =>
  transaction.amount < 0 ? -transaction.amount : transaction.amount;

export const toDateKey = (value: Date | string) =>
  format(typeof value === 'string' ? parseISO(value) : value, 'yyyy-MM-dd');

export const buildTransactionDaySummaries = (transactions: Transaction[]) => {
  const summaries = new Map<string, TransactionDaySummary>();

  for (const transaction of transactions) {
    const occurredAt = parseISO(transaction.occurredAt);
    const dateKey = toDateKey(occurredAt);
    const isIncome = transaction.transactionType === 'Income';
    const existing = summaries.get(dateKey);

    if (existing) {
      existing.transactions.push(transaction);
      existing.transactionCount += 1;
      if (isIncome) {
        existing.incomeTotal += transaction.amount;
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
      incomeTotal: isIncome ? transaction.amount : 0,
      outflowTotal: isIncome ? 0 : getOutflowMagnitude(transaction),
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