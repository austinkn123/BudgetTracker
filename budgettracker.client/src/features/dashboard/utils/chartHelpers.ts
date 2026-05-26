import type { Transaction, BudgetPlan, Category } from '../../../shared/types/api';
import { format, parseISO, startOfMonth, subMonths } from 'date-fns';

export interface SummaryTotals {
  totalIncome: number;
  totalExpenses: number;
  netBalance: number;
}

export interface CategorySlice {
  label: string;
  value: number;
  id: number;
}

export interface MonthlyDataPoint {
  month: string;
  income: number;
  expenses: number;
}

export interface BudgetVsActualItem {
  category: string;
  budgeted: number;
  actual: number;
}

// BUD-18: Transaction.amount is signed. Expense/Transfer rows carry a negative
// magnitude on the wire; the dashboard aggregates use unsigned magnitudes so
// "Total Expenses" reads as a positive dollar figure and netBalance subtracts
// cleanly. The single helper below centralizes that convention.
const expenseMagnitude = (amount: number) => (amount < 0 ? -amount : amount);

export function computeSummaryTotals(transactions: Transaction[]): SummaryTotals {
  let totalIncome = 0;
  let totalExpenses = 0;

  for (const t of transactions) {
    if (t.transactionType === 'Income') {
      totalIncome += t.amount;
    } else {
      totalExpenses += expenseMagnitude(t.amount);
    }
  }

  return { totalIncome, totalExpenses, netBalance: totalIncome - totalExpenses };
}

export function computeSummaryTotalsFromPlan(plan: BudgetPlan): SummaryTotals {
  const totalIncome = plan.netIncomeMonthly;

  const totalExpenses = plan.entries
    .filter((line) => line.lineType === 'Expense')
    .reduce((sum, line) => sum + line.monthlyEquivalent, 0);

  return { totalIncome, totalExpenses, netBalance: totalIncome - totalExpenses };
}

export function aggregateByCategoryFromPlan(
  plan: BudgetPlan,
  categories: Category[],
): CategorySlice[] {
  const categoryMap = new Map(categories.map((c) => [c.id, c.name]));
  const totals = new Map<number, number>();

  for (const line of plan.entries) {
    if (line.lineType !== 'Expense' || line.categoryId == null) {
      continue;
    }

    totals.set(line.categoryId, (totals.get(line.categoryId) ?? 0) + line.monthlyEquivalent);
  }

  return Array.from(totals.entries())
    .map(([id, value]) => ({
      id,
      label: categoryMap.get(id) ?? `Category ${id}`,
      value,
    }))
    .sort((a, b) => b.value - a.value);
}

export function aggregateByCategory(
  transactions: Transaction[],
  categories: Category[],
): CategorySlice[] {
  const categoryMap = new Map(categories.map((c) => [c.id, c.name]));
  const totals = new Map<number, number>();

  for (const t of transactions) {
    if (t.transactionType === 'Expense') {
      totals.set(t.categoryId, (totals.get(t.categoryId) ?? 0) + expenseMagnitude(t.amount));
    }
  }

  return Array.from(totals.entries())
    .map(([id, value]) => ({
      id,
      label: categoryMap.get(id) ?? `Category ${id}`,
      value,
    }))
    .sort((a, b) => b.value - a.value);
}

export function aggregateByMonth(transactions: Transaction[]): MonthlyDataPoint[] {
  const monthMap = new Map<string, { income: number; expenses: number }>();

  for (const t of transactions) {
    const month = format(parseISO(t.occurredAt), 'yyyy-MM');
    const existing = monthMap.get(month) ?? { income: 0, expenses: 0 };

    if (t.transactionType === 'Income') {
      existing.income += t.amount;
    } else {
      existing.expenses += expenseMagnitude(t.amount);
    }

    monthMap.set(month, existing);
  }

  return Array.from(monthMap.entries())
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([month, data]) => ({
      month: format(parseISO(`${month}-01`), 'MMM yyyy'),
      income: data.income,
      expenses: data.expenses,
    }));
}

export function budgetVsActual(
  activePlan: BudgetPlan | undefined,
  transactions: Transaction[],
  categories: Category[],
): BudgetVsActualItem[] {
  if (!activePlan) return [];

  const categoryMap = new Map(categories.map((c) => [c.id, c.name]));

  const planMonth = activePlan.planMonth.substring(0, 7); // 'yyyy-MM'
  const monthTransactions = transactions.filter(
    (t) => t.occurredAt.substring(0, 7) === planMonth && t.transactionType === 'Expense',
  );

  const actualByCategory = new Map<number, number>();
  for (const t of monthTransactions) {
    actualByCategory.set(
      t.categoryId,
      (actualByCategory.get(t.categoryId) ?? 0) + expenseMagnitude(t.amount),
    );
  }

  const budgetedByCategory = new Map<number, number>();
  for (const line of activePlan.entries) {
    if (line.lineType === 'Expense' && line.categoryId != null) {
      budgetedByCategory.set(
        line.categoryId,
        (budgetedByCategory.get(line.categoryId) ?? 0) + line.monthlyEquivalent,
      );
    }
  }

  const allCategoryIds = new Set([
    ...budgetedByCategory.keys(),
    ...actualByCategory.keys(),
  ]);

  return Array.from(allCategoryIds)
    .map((id) => ({
      category: categoryMap.get(id) ?? `Category ${id}`,
      budgeted: budgetedByCategory.get(id) ?? 0,
      actual: actualByCategory.get(id) ?? 0,
    }))
    .sort((a, b) => b.budgeted - a.budgeted);
}

/**
 * Filter a list of transactions to those occurring within `[start, end]`
 * (inclusive on both ends — matches the {@link useDateRange} bounds).
 */
export function filterTransactionsByRange(
  transactions: Transaction[],
  start: Date,
  end: Date,
): Transaction[] {
  const startMs = start.getTime();
  const endMs = end.getTime();
  return transactions.filter((t) => {
    const ts = parseISO(t.occurredAt).getTime();
    return ts >= startMs && ts <= endMs;
  });
}

/**
 * Build a per-category, per-month expense timeline for the past `monthsBack`
 * months (inclusive of the current month). Each map entry is a chronologically
 * ordered series suitable for a sparkline.
 */
export function aggregateByCategoryMonthly(
  transactions: Transaction[],
  categories: Category[],
  monthsBack: number,
): Map<number, MonthlyDataPoint[]> {
  const now = new Date();
  // Build the month bucket order: oldest -> newest.
  const months: string[] = [];
  for (let i = monthsBack - 1; i >= 0; i -= 1) {
    months.push(format(startOfMonth(subMonths(now, i)), 'yyyy-MM'));
  }
  const monthLabels = new Map(
    months.map((m) => [m, format(parseISO(`${m}-01`), 'MMM yyyy')] as const),
  );

  const result = new Map<number, MonthlyDataPoint[]>();

  // Seed every category with zeroed months so sparklines have a flat baseline.
  for (const c of categories) {
    result.set(
      c.id,
      months.map((m) => ({ month: monthLabels.get(m)!, income: 0, expenses: 0 })),
    );
  }

  for (const t of transactions) {
    const monthKey = t.occurredAt.substring(0, 7);
    const idx = months.indexOf(monthKey);
    if (idx === -1) continue;

    let series = result.get(t.categoryId);
    if (!series) {
      series = months.map((m) => ({ month: monthLabels.get(m)!, income: 0, expenses: 0 }));
      result.set(t.categoryId, series);
    }

    if (t.transactionType === 'Income') {
      series[idx].income += t.amount;
    } else {
      series[idx].expenses += expenseMagnitude(t.amount);
    }
  }

  return result;
}
