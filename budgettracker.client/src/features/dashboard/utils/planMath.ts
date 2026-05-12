import {
  endOfMonth,
  format,
  getDate,
  getDaysInMonth,
  isSameMonth,
  parseISO,
  startOfMonth,
} from 'date-fns';
import type { BudgetPlan, Category, Transaction } from '../../../shared/types/api';

/** Output of {@link daysElapsedInMonth}. */
export interface DaysElapsed {
  daysElapsed: number;
  daysInMonth: number;
  daysPct: number;
}

/**
 * Compute how far we are through `planMonth` as of `now`.
 * If `now` is before the plan month we return `daysElapsed = 0`; if after, the full month.
 * `daysPct` is in [0, 1].
 */
export function daysElapsedInMonth(now: Date, planMonth: Date): DaysElapsed {
  const daysInMonth = getDaysInMonth(planMonth);
  const start = startOfMonth(planMonth);
  const end = endOfMonth(planMonth);

  let daysElapsed: number;
  if (now < start) {
    daysElapsed = 0;
  } else if (now > end) {
    daysElapsed = daysInMonth;
  } else if (isSameMonth(now, planMonth)) {
    daysElapsed = getDate(now);
  } else {
    daysElapsed = daysInMonth;
  }

  const daysPct = daysInMonth === 0 ? 0 : daysElapsed / daysInMonth;
  return { daysElapsed, daysInMonth, daysPct };
}

/**
 * Linearly project month-end spend from the run-rate so far.
 * If no days have elapsed yet, we cannot project — return the actual amount.
 */
export function projectMonthEndSpend(
  actual: number,
  daysElapsed: number,
  daysInMonth: number,
): number {
  if (daysElapsed <= 0) return actual;
  if (daysInMonth <= 0) return actual;
  return (actual / daysElapsed) * daysInMonth;
}

/**
 * Warm, encouraging headline based on the pacing delta
 * (spentPct - daysPct). Negative delta = ahead of pace; positive = behind.
 */
export function getStatusHeadline(
  spentPct: number,
  daysPct: number,
  planMonth: Date,
): string {
  const monthName = format(planMonth, 'MMMM');
  const delta = spentPct - daysPct;

  if (daysPct === 0) return `${monthName} is just getting started`;
  if (delta <= -0.15) return `You're cruising in ${monthName}`;
  if (delta <= -0.05) return `Comfortably ahead in ${monthName}`;
  if (delta < 0.05) return `Right on the trail for ${monthName}`;
  if (delta < 0.15) return `A little brisk in ${monthName}`;
  if (delta < 0.3) return `Tightening up needed in ${monthName}`;
  return `Off the trail in ${monthName}`;
}

/** A single bar in the cashflow waterfall chart. */
export interface WaterfallItem {
  label: string;
  /** Visible bar height (always non-negative when stacked). */
  value: number;
  /** Whether this item represents money in, money out, or the net total. */
  kind: 'income' | 'expense' | 'net';
  /** Signed amount (income positive, expense negative, net signed). */
  signed: number;
}

/**
 * Build the data for a waterfall chart for the plan's month:
 * income → top 5 expense categories → "Other" expenses → net.
 */
export function buildWaterfall(
  transactions: Transaction[],
  plan: BudgetPlan | undefined,
  categories: Category[],
): WaterfallItem[] {
  if (!plan) return [];

  const planMonth = parseISO(plan.planMonth);
  const monthKey = format(planMonth, 'yyyy-MM');
  const monthTxns = transactions.filter((t) => t.occurredAt.substring(0, 7) === monthKey);

  const income = monthTxns
    .filter((t) => t.transactionType === 'Income')
    .reduce((sum, t) => sum + t.amount, 0);

  const expensesByCategory = new Map<number, number>();
  for (const t of monthTxns) {
    if (t.transactionType !== 'Expense') continue;
    expensesByCategory.set(t.categoryId, (expensesByCategory.get(t.categoryId) ?? 0) + t.amount);
  }

  const categoryMap = new Map(categories.map((c) => [c.id, c.name]));

  const ranked = Array.from(expensesByCategory.entries())
    .map(([id, value]) => ({
      id,
      label: categoryMap.get(id) ?? `Category ${id}`,
      value,
    }))
    .sort((a, b) => b.value - a.value);

  const top = ranked.slice(0, 5);
  const otherTotal = ranked.slice(5).reduce((sum, r) => sum + r.value, 0);
  const totalExpenses = ranked.reduce((sum, r) => sum + r.value, 0);
  const net = income - totalExpenses;

  const items: WaterfallItem[] = [
    { label: 'Income', value: income, kind: 'income', signed: income },
    ...top.map<WaterfallItem>((t) => ({
      label: t.label,
      value: t.value,
      kind: 'expense',
      signed: -t.value,
    })),
  ];

  if (otherTotal > 0) {
    items.push({ label: 'Other', value: otherTotal, kind: 'expense', signed: -otherTotal });
  }

  items.push({ label: 'Net', value: Math.abs(net), kind: 'net', signed: net });

  return items;
}

/** A summary row for one budget bucket. */
export interface BucketSummary {
  bucket: 'Core' | 'Buffer';
  planned: number;
  actual: number;
}

/**
 * Aggregate planned amount per bucket from the budget plan and pair it
 * with actual expense totals (by category) for the plan's month.
 */
export function aggregateByBucket(
  plan: BudgetPlan | undefined,
  transactions: Transaction[],
): BucketSummary[] {
  if (!plan) return [];

  const planMonth = plan.planMonth.substring(0, 7);
  const monthTxns = transactions.filter(
    (t) => t.occurredAt.substring(0, 7) === planMonth && t.transactionType === 'Expense',
  );

  // Map each expense category in the plan to its bucket.
  const bucketByCategory = new Map<number, 'Core' | 'Buffer'>();
  const planned: Record<'Core' | 'Buffer', number> = { Core: 0, Buffer: 0 };

  for (const line of plan.entries) {
    if (line.lineType !== 'Expense') continue;
    planned[line.bucket] += line.monthlyEquivalent;
    if (line.categoryId != null) {
      bucketByCategory.set(line.categoryId, line.bucket);
    }
  }

  const actual: Record<'Core' | 'Buffer', number> = { Core: 0, Buffer: 0 };
  for (const t of monthTxns) {
    const bucket = bucketByCategory.get(t.categoryId);
    // Transactions whose category isn't represented in the plan fall back to Buffer.
    actual[bucket ?? 'Buffer'] += t.amount;
  }

  return [
    { bucket: 'Core', planned: planned.Core, actual: actual.Core },
    { bucket: 'Buffer', planned: planned.Buffer, actual: actual.Buffer },
  ];
}
