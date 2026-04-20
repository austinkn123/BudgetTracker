import type { Transaction, BudgetPlan, Category } from '../../../shared/types/api';
import { format, parseISO } from 'date-fns';

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

export function computeSummaryTotals(transactions: Transaction[]): SummaryTotals {
  let totalIncome = 0;
  let totalExpenses = 0;

  for (const t of transactions) {
    if (t.transactionType === 'Income') {
      totalIncome += t.amount;
    } else {
      totalExpenses += t.amount;
    }
  }

  return { totalIncome, totalExpenses, netBalance: totalIncome - totalExpenses };
}

export function computeSummaryTotalsFromPlan(plan: BudgetPlan): SummaryTotals {
  const totalIncome = plan.netIncomeMonthly;

  const totalExpenses = plan.lines
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

  for (const line of plan.lines) {
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
      totals.set(t.categoryId, (totals.get(t.categoryId) ?? 0) + t.amount);
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
      existing.expenses += t.amount;
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
    actualByCategory.set(t.categoryId, (actualByCategory.get(t.categoryId) ?? 0) + t.amount);
  }

  const budgetedByCategory = new Map<number, number>();
  for (const line of activePlan.lines) {
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
