import type {
  CategoryMonthSpend,
  CategoryPerformance,
  CategorySpend,
  PlanPerformance,
  Transaction,
} from '../../../shared/types/api';

/**
 * Presentation selection over the server's complete budget analysis.
 *
 * The server computes every number; these helpers only choose which to show —
 * sorting, truncating, folding a remainder into "Other", and attaching display
 * names. Keep arithmetic out of here beyond summing an already-computed tail.
 *
 * A null categoryId means "uncategorized"; `key` exists because null is not a
 * usable React key.
 */

export interface DriftingCategory {
  key: string;
  name: string;
  planned: number;
  actual: number;
  overBy: number;
}

export interface WaterfallBar {
  label: string;
  /** Bar height — always non-negative. */
  value: number;
  kind: 'income' | 'expense' | 'net';
  /** Signed amount: income positive, expense negative, net either. */
  signed: number;
}

export interface SpendSlice {
  key: string;
  label: string;
  value: number;
}

export interface CategoryCard {
  key: string;
  name: string;
  planned: number;
  actual: number;
  /** Expense magnitudes, oldest -> newest. */
  monthly: number[];
  transactions: Transaction[];
}

export type CategoryNames = Map<number, string>;

const categoryKey = (categoryId: number | null): string =>
  categoryId === null ? 'uncategorized' : String(categoryId);

export const categoryLabel = (names: CategoryNames, categoryId: number | null): string => {
  if (categoryId === null) return 'Uncategorized';
  return names.get(categoryId) ?? `Category ${categoryId}`;
};

const transactionCategoryId = (transaction: Transaction): number | null =>
  transaction.categoryId ?? null;

const byOccurredAtDesc = (a: Transaction, b: Transaction) =>
  b.occurredAt.localeCompare(a.occurredAt);

/** Categories running over their planned amount, worst first. */
export const selectDrifting = (
  byCategory: CategoryPerformance[],
  names: CategoryNames,
  limit: number,
): DriftingCategory[] =>
  byCategory
    .filter((c) => c.overBy > 0)
    .sort((a, b) => b.overBy - a.overBy)
    .slice(0, limit)
    .map((c) => ({
      key: categoryKey(c.categoryId),
      name: categoryLabel(names, c.categoryId),
      planned: c.planned,
      actual: c.actual,
      overBy: c.overBy,
    }));

/** Biggest spend categories, with everything past `limit` folded into a single "Other". */
export const selectTopSpend = (
  spend: CategorySpend[],
  names: CategoryNames,
  limit: number,
): SpendSlice[] => {
  const ranked = [...spend]
    .filter((s) => s.amount > 0)
    .sort((a, b) => b.amount - a.amount);

  const top: SpendSlice[] = ranked.slice(0, limit).map((s) => ({
    key: categoryKey(s.categoryId),
    label: categoryLabel(names, s.categoryId),
    value: s.amount,
  }));

  const otherTotal = ranked.slice(limit).reduce((sum, s) => sum + s.amount, 0);
  if (otherTotal > 0) {
    top.push({ key: 'other', label: 'Other', value: otherTotal });
  }
  return top;
};

/** Income, then the biggest expense categories, then "Other", then the net result. */
export const selectWaterfall = (
  planMonth: PlanPerformance,
  names: CategoryNames,
  limit: number,
): WaterfallBar[] => {
  const ranked = planMonth.byCategory
    .filter((c) => c.actual > 0)
    .sort((a, b) => b.actual - a.actual);

  const bars: WaterfallBar[] = [
    { label: 'Income', value: planMonth.income, kind: 'income', signed: planMonth.income },
    ...ranked.slice(0, limit).map<WaterfallBar>((c) => ({
      label: categoryLabel(names, c.categoryId),
      value: c.actual,
      kind: 'expense',
      signed: -c.actual,
    })),
  ];

  const otherTotal = ranked.slice(limit).reduce((sum, c) => sum + c.actual, 0);
  if (otherTotal > 0) {
    bars.push({ label: 'Other', value: otherTotal, kind: 'expense', signed: -otherTotal });
  }

  const net = planMonth.income - planMonth.expenses;
  bars.push({ label: 'Net', value: Math.abs(net), kind: 'net', signed: net });
  return bars;
};

/** Per-category cards: planned vs actual, a sparkline series, and the window's transactions. */
export const selectCategoryCards = (
  planMonth: PlanPerformance,
  monthlyTrend: CategoryMonthSpend[],
  windowTransactions: Transaction[],
  names: CategoryNames,
  transactionLimit: number,
): CategoryCard[] => {
  const trendByCategory = new Map<string, CategoryMonthSpend[]>();
  for (const point of monthlyTrend) {
    const key = categoryKey(point.categoryId);
    const series = trendByCategory.get(key) ?? [];
    series.push(point);
    trendByCategory.set(key, series);
  }

  const transactionsByCategory = new Map<string, Transaction[]>();
  for (const transaction of windowTransactions) {
    const key = categoryKey(transactionCategoryId(transaction));
    const list = transactionsByCategory.get(key) ?? [];
    list.push(transaction);
    transactionsByCategory.set(key, list);
  }

  return [...planMonth.byCategory]
    .sort((a, b) => b.planned - a.planned || b.actual - a.actual)
    .map((c) => {
      const key = categoryKey(c.categoryId);
      return {
        key,
        name: categoryLabel(names, c.categoryId),
        planned: c.planned,
        actual: c.actual,
        monthly: (trendByCategory.get(key) ?? [])
          .slice()
          .sort((a, b) => a.month.localeCompare(b.month))
          .map((point) => point.expenses),
        transactions: (transactionsByCategory.get(key) ?? [])
          .slice()
          .sort(byOccurredAtDesc)
          .slice(0, transactionLimit),
      };
    });
};

/** Most recent transactions in the window, newest first. */
export const selectRecentActivity = (
  windowTransactions: Transaction[],
  limit: number,
): Transaction[] => [...windowTransactions].sort(byOccurredAtDesc).slice(0, limit);
