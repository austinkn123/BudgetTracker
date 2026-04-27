import {
  endOfDay,
  endOfMonth,
  format,
  parse,
  parseISO,
  startOfDay,
  startOfMonth,
  startOfYear,
  subMonths,
} from 'date-fns';
import type { Transaction } from '../../../shared/types/api';

export type TransactionGroupBy = 'day' | 'month';
export type TransactionRangePreset = 'this-month' | 'last-3-months' | 'ytd' | 'all' | 'custom';

export type TransactionRangeValues = {
  startDate: string;
  endDate: string;
  startMonth: string;
  endMonth: string;
};

export type TransactionGroup = {
  key: string;
  label: string;
  transactions: Transaction[];
  incomeTotal: number;
  outflowTotal: number;
  netTotal: number;
  transactionCount: number;
};

type GroupTransactionsOptions = {
  groupBy: TransactionGroupBy;
  startValue: string;
  endValue: string;
};

const getSignedAmount = (transaction: Transaction) =>
  transaction.transactionType === 'Income' ? transaction.amount : -transaction.amount;

const parseMonthInput = (value: string) => parse(`${value}-01`, 'yyyy-MM-dd', new Date());

const normalizeRange = (
  groupBy: TransactionGroupBy,
  startValue: string,
  endValue: string,
): { start: Date | null; end: Date | null } => {
  const rawStart = startValue
    ? groupBy === 'month'
      ? startOfMonth(parseMonthInput(startValue))
      : startOfDay(parseISO(startValue))
    : null;

  const rawEnd = endValue
    ? groupBy === 'month'
      ? endOfMonth(parseMonthInput(endValue))
      : endOfDay(parseISO(endValue))
    : null;

  if (rawStart && rawEnd && rawStart > rawEnd) {
    return { start: rawEnd, end: rawStart };
  }

  return { start: rawStart, end: rawEnd };
};

export const getRangeValuesForPreset = (
  preset: Exclude<TransactionRangePreset, 'custom'>,
  referenceDate = new Date(),
): TransactionRangeValues => {
  if (preset === 'all') {
    return {
      startDate: '',
      endDate: '',
      startMonth: '',
      endMonth: '',
    };
  }

  const monthStart =
    preset === 'this-month'
      ? startOfMonth(referenceDate)
      : preset === 'last-3-months'
        ? startOfMonth(subMonths(referenceDate, 2))
        : startOfYear(referenceDate);

  const dayEnd = endOfDay(referenceDate);
  const monthEnd = endOfMonth(referenceDate);

  return {
    startDate: format(monthStart, 'yyyy-MM-dd'),
    endDate: format(dayEnd, 'yyyy-MM-dd'),
    startMonth: format(monthStart, 'yyyy-MM'),
    endMonth: format(monthEnd, 'yyyy-MM'),
  };
};

export const groupTransactionsByPeriod = (
  transactions: Transaction[],
  { groupBy, startValue, endValue }: GroupTransactionsOptions,
): { groups: TransactionGroup[]; visibleCount: number } => {
  const { start, end } = normalizeRange(groupBy, startValue, endValue);

  const filtered = transactions.filter((transaction) => {
    const occurredAt = parseISO(transaction.occurredAt);

    if (start && occurredAt < start) return false;
    if (end && occurredAt > end) return false;

    return true;
  });

  const groups = new Map<string, TransactionGroup>();

  for (const transaction of filtered) {
    const occurredAt = parseISO(transaction.occurredAt);
    const key = format(occurredAt, groupBy === 'month' ? 'yyyy-MM' : 'yyyy-MM-dd');
    const label = format(occurredAt, groupBy === 'month' ? 'MMMM yyyy' : 'PPPP');
    const signedAmount = getSignedAmount(transaction);

    const existing = groups.get(key);
    if (existing) {
      existing.transactions.push(transaction);
      existing.transactionCount += 1;
      if (transaction.transactionType === 'Income') {
        existing.incomeTotal += transaction.amount;
      } else {
        existing.outflowTotal += transaction.amount;
      }
      existing.netTotal += signedAmount;
      continue;
    }

    groups.set(key, {
      key,
      label,
      transactions: [transaction],
      transactionCount: 1,
      incomeTotal: transaction.transactionType === 'Income' ? transaction.amount : 0,
      outflowTotal: transaction.transactionType === 'Income' ? 0 : transaction.amount,
      netTotal: signedAmount,
    });
  }

  const sortedGroups = Array.from(groups.values())
    .sort((left, right) => right.key.localeCompare(left.key))
    .map((group) => ({
      ...group,
      transactions: group.transactions
        .slice()
        .sort((left, right) => right.occurredAt.localeCompare(left.occurredAt)),
    }));

  return {
    groups: sortedGroups,
    visibleCount: filtered.length,
  };
};