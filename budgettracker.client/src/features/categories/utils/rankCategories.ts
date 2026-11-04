import type { BudgetPlan, Category, Transaction } from '../../../shared/types/api';

export type CategorySegment = 'spending' | 'income' | 'transfers';

const SEGMENT_TYPES: Record<CategorySegment, readonly string[]> = {
  spending: ['Expense', 'Both'],
  income: ['Income'],
  transfers: ['Transfer'],
};

// Money out is negative, so spending flips the sign and a refund nets against it.
// Own-account transfers move both ways; what matters is how much moved.
const VALUE: Record<CategorySegment, (amount: number) => number> = {
  spending: (amount) => -amount,
  income: (amount) => amount,
  transfers: (amount) => Math.abs(amount),
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
  transactions: Transaction[],
  plans: BudgetPlan[],
  segment: CategorySegment,
  since: Date,
): RankedCategory[] => {
  const rows = new Map<number, RankedCategory>(
    categories
      .filter((c) => SEGMENT_TYPES[segment].includes(c.categoryType))
      .map((c) => [c.id, { category: c, total: 0, transactions: 0, planEntries: 0, inActivePlan: false }]),
  );

  const sinceMs = since.getTime();
  for (const t of transactions) {
    const row = t.categoryId == null ? undefined : rows.get(t.categoryId);
    if (!row) continue;
    row.transactions += 1;
    if (new Date(t.occurredAt).getTime() >= sinceMs) row.total += VALUE[segment](t.amount);
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
