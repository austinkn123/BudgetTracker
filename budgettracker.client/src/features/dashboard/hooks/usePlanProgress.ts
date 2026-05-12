import { useMemo } from 'react';
import { parseISO } from 'date-fns';
import type { BudgetPlan, Category, Transaction } from '../../../shared/types/api';
import {
  daysElapsedInMonth,
  getStatusHeadline,
  projectMonthEndSpend,
} from '../utils/planMath';

/** A category trending over its planned amount this month. */
export interface DriftingCategory {
  categoryId: number;
  name: string;
  planned: number;
  actual: number;
  overBy: number;
}

export interface PlanProgress {
  daysElapsed: number;
  daysInMonth: number;
  /** Fraction in [0, 1] of the plan month elapsed. */
  daysPct: number;
  /** Fraction in [0, 1] of the budgeted expense total that has been spent. */
  spentPct: number;
  /** Linearly projected end-of-month expense total. */
  projectedEnd: number;
  /** spentPct - daysPct. Positive = spending faster than time. */
  pacingDelta: number;
  /** Coarse status bucket. */
  status: 'ahead' | 'on-track' | 'behind';
  headline: string;
  /** Top 3 categories ordered by `overBy` (descending). */
  driftingCategories: DriftingCategory[];
  /** Total expense actual for the plan month. */
  actualExpenses: number;
  /** Total expense budgeted for the plan month. */
  plannedExpenses: number;
  /** Remaining dollars on the plan (planned - actual). */
  remaining: number;
  /** Dollar/day required to stay on plan through month end. */
  perDiemToStay: number;
}

interface UsePlanProgressArgs {
  plan: BudgetPlan | undefined;
  transactions: Transaction[];
  categories: Category[];
  now?: Date;
}

/**
 * Pure derivation of the plan-progress snapshot for the active plan.
 * Memoized on its inputs so consumers can render the hero cheaply.
 */
export function usePlanProgress({
  plan,
  transactions,
  categories,
  now,
}: UsePlanProgressArgs): PlanProgress | null {
  return useMemo(() => {
    if (!plan) return null;

    const currentDate = now ?? new Date();
    const planMonth = parseISO(plan.planMonth);
    const monthKey = plan.planMonth.substring(0, 7);

    const { daysElapsed, daysInMonth, daysPct } = daysElapsedInMonth(currentDate, planMonth);

    // Build planned-by-category map.
    const plannedByCategory = new Map<number, number>();
    let plannedExpenses = 0;
    for (const line of plan.entries) {
      if (line.lineType !== 'Expense') continue;
      plannedExpenses += line.monthlyEquivalent;
      if (line.categoryId != null) {
        plannedByCategory.set(
          line.categoryId,
          (plannedByCategory.get(line.categoryId) ?? 0) + line.monthlyEquivalent,
        );
      }
    }

    // Aggregate actuals for the plan's month.
    const actualByCategory = new Map<number, number>();
    let actualExpenses = 0;
    for (const t of transactions) {
      if (t.occurredAt.substring(0, 7) !== monthKey) continue;
      if (t.transactionType !== 'Expense') continue;
      actualExpenses += t.amount;
      actualByCategory.set(t.categoryId, (actualByCategory.get(t.categoryId) ?? 0) + t.amount);
    }

    const spentPct = plannedExpenses > 0 ? actualExpenses / plannedExpenses : 0;
    const projectedEnd = projectMonthEndSpend(actualExpenses, daysElapsed, daysInMonth);
    const pacingDelta = spentPct - daysPct;

    let status: PlanProgress['status'] = 'on-track';
    if (pacingDelta <= -0.05) status = 'ahead';
    else if (pacingDelta >= 0.05) status = 'behind';

    const headline = getStatusHeadline(spentPct, daysPct, planMonth);

    const categoryNames = new Map(categories.map((c) => [c.id, c.name]));
    const allCategoryIds = new Set<number>([
      ...plannedByCategory.keys(),
      ...actualByCategory.keys(),
    ]);

    const driftingCategories: DriftingCategory[] = Array.from(allCategoryIds)
      .map((id) => {
        const planned = plannedByCategory.get(id) ?? 0;
        const actual = actualByCategory.get(id) ?? 0;
        return {
          categoryId: id,
          name: categoryNames.get(id) ?? `Category ${id}`,
          planned,
          actual,
          overBy: actual - planned,
        };
      })
      .filter((c) => c.overBy > 0)
      .sort((a, b) => b.overBy - a.overBy)
      .slice(0, 3);

    const remaining = plannedExpenses - actualExpenses;
    const daysLeft = Math.max(daysInMonth - daysElapsed, 0);
    const perDiemToStay = daysLeft > 0 ? remaining / daysLeft : 0;

    return {
      daysElapsed,
      daysInMonth,
      daysPct,
      spentPct,
      projectedEnd,
      pacingDelta,
      status,
      headline,
      driftingCategories,
      actualExpenses,
      plannedExpenses,
      remaining,
      perDiemToStay,
    };
  }, [plan, transactions, categories, now]);
}
