import { addDays, format, startOfDay, subMonths } from 'date-fns';
import type { BudgetPlan, Category, CategoryUsage } from '../../../shared/types/api';

export type CategorySegment = 'spending' | 'income' | 'transfers';

const SEGMENT_TYPES: Record<CategorySegment, readonly string[]> = {
  spending: ['Expense', 'Both'],
  income: ['Income'],
  transfers: ['Transfer'],
};

// Money out is negative (outflowSince <= 0), so spending flips the sign of the net and a refund
// nets against it. Own-account transfers move both ways; what matters is how much moved, which is
// the sum of the magnitudes: inflow minus a non-positive outflow.
const VALUE: Record<CategorySegment, (usage: CategoryUsage) => number> = {
  spending: (u) => -(u.inflowSince + u.outflowSince),
  income: (u) => u.inflowSince + u.outflowSince,
  transfers: (u) => u.inflowSince - u.outflowSince,
};

/**
 * The `since` date for a window of `months` ending now, as the calendar date the server reads.
 *
 * Rows sit at midnight, and the window used to be compared as instants (`occurredAt >= now - 12
 * months`), so the first date in it is the day after that instant's date — unless the instant is
 * itself a midnight. A date string also keeps the query key stable for the whole day.
 */
export const usageSince = (now: Date, months = 12): string => {
  const start = subMonths(now, months);
  const day = startOfDay(start);
  return format(day.getTime() === start.getTime() ? day : addDays(day, 1), 'yyyy-MM-dd');
};

export interface RankedCategory {
  category: Category;
  /** Signed total since the window start, in the segment's direction. */
  total: number;
  /** Every transaction filed here, any date: this is what a delete would orphan. */
  transactions: number;
  /** Lines across every plan, active or not. */
  planEntries: number;
  inActivePlan: boolean;
}

/** One segment's categories, biggest first; unused ones sink to the bottom by name. */
export const rankCategories = (
  categories: Category[],
  usage: CategoryUsage[],
  plans: BudgetPlan[],
  segment: CategorySegment,
): RankedCategory[] => {
  const rows = new Map<number, RankedCategory>(
    categories
      .filter((c) => SEGMENT_TYPES[segment].includes(c.categoryType))
      .map((c) => [c.id, { category: c, total: 0, transactions: 0, planEntries: 0, inActivePlan: false }]),
  );

  // The window is applied server-side: usage carries in/out sums since the window start.
  for (const u of usage) {
    const row = rows.get(u.categoryId);
    if (!row) continue;
    row.transactions += u.transactionCount;
    row.total += VALUE[segment](u);
  }

  for (const plan of plans) {
    for (const entry of plan.entries) {
      const row = entry.categoryId == null ? undefined : rows.get(entry.categoryId);
      if (!row) continue;
      row.planEntries += 1;
      row.inActivePlan ||= plan.isActive;
    }
  }

  return [...rows.values()].sort(
    (a, b) => b.total - a.total || a.category.name.localeCompare(b.category.name),
  );
};
