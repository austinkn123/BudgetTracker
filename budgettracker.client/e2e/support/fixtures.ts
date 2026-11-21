// Typed against the app's own API types, so a contract change breaks these fixtures at compile time.
import type {
  BudgetAnalysis,
  BudgetPlan,
  CashFlowReport,
  Category,
  CategoryRule,
  CategoryUsage,
  MoneyMap,
  Transaction,
  TransactionPage,
  TransactionSummary,
  User,
} from '../../src/shared/types/api';
import { CHASE, ALLY } from '../../src/test/plaidFixtures';
import { holdingsView, tradesView, webullView } from '../../src/test/webullFixtures';

const month = (() => {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
})();
const day = (d: number) => `${month}-${String(d).padStart(2, '0')}T00:00:00`;

export const categories: Category[] = [
  { id: 1, userId: 1, name: 'Groceries', categoryType: 'Expense', plaidCategoryPrimary: 'FOOD_AND_DRINK' },
  { id: 2, userId: 1, name: 'Rent & Mortgage Payment (Primary Residence)', categoryType: 'Expense' },
  { id: 3, userId: 1, name: 'Rideshare', categoryType: 'Expense' },
  { id: 4, userId: 1, name: 'Salary', categoryType: 'Income' },
];

// Long payees and large amounts on purpose: the narrowest layouts have to survive them.
export const transactions: Transaction[] = [
  { id: 1, accountId: 1, transactionType: 'Expense', categoryId: null, amount: -18.4, occurredAt: day(18), payee: 'UBER 072515 SF**POOL** TRIP HELP.UBER.COM CA', createdAt: day(18), isImported: true, isPending: true, plaidAccountId: CHASE.accounts[0].plaidAccountId, plaidCategoryPrimary: 'TRANSPORTATION' },
  { id: 2, accountId: 1, transactionType: 'Expense', categoryId: 1, amount: -1234.56, occurredAt: day(15), payee: 'WHOLE FOODS MARKET #10234 KATY TX', notes: 'Hosting the whole family for Thanksgiving dinner this year', createdAt: day(15), isImported: true, plaidAccountId: CHASE.accounts[0].plaidAccountId, plaidCategoryPrimary: 'FOOD_AND_DRINK' },
  { id: 3, accountId: 1, transactionType: 'Income', categoryId: 4, amount: 12345.67, occurredAt: day(1), payee: 'ACME CORP PAYROLL DIRECT DEP', createdAt: day(1), isImported: true, plaidAccountId: CHASE.accounts[0].plaidAccountId, plaidCategoryPrimary: 'INCOME' },
  { id: 5, accountId: 1, transactionType: 'Expense', categoryId: null, amount: -86.12, occurredAt: day(12), payee: "TRADER JOE'S #123", createdAt: day(12), isImported: true, plaidAccountId: CHASE.accounts[0].plaidAccountId, plaidCategoryPrimary: 'FOOD_AND_DRINK' },
  { id: 4, accountId: 1, transactionType: 'Expense', categoryId: 2, amount: -2450, occurredAt: day(1), payee: 'Rent', createdAt: day(1), isImported: true, plaidAccountId: CHASE.accounts[0].plaidAccountId },
];

export const transactionPage: TransactionPage = { items: transactions, nextCursor: null };
export const transactionSummary: TransactionSummary = { uncategorizedCount: 2, pendingByAccount: [{ plaidAccountId: CHASE.accounts[0].plaidAccountId, amount: -18.4 }] };
export const user: User = { id: 1, email: 'e2e@example.test', createdAt: day(1) };
export const categoryUsage: CategoryUsage[] = categories.map((c) => ({ categoryId: c.id, transactionCount: 3, inflowSince: 0, outflowSince: -500 }));
export const categoryRules: CategoryRule[] = [
  { id: 1, pattern: 'uber', matchType: 'Contains', categoryId: 3, categoryName: 'Rideshare', matchedCount: 12 },
  { id: 2, pattern: 'WHOLE FOODS MARKET #10234 KATY TX', matchType: 'Exact', categoryId: 1, categoryName: 'Groceries', matchedCount: 4 },
];

export const budgetPlans: BudgetPlan[] = [
  {
    id: 1, userId: 1, name: 'Household plan', planMonth: `${month}-01`, netIncomeMonthly: 12345.67, isActive: true, createdAt: day(1),
    entries: [
      { id: 1, budgetPlanId: 1, categoryId: 2, lineType: 'Expense', bucket: 'Core', cadence: 'Monthly', amount: 2450, monthlyEquivalent: 2450, isStressFactor: false, sortOrder: 1, createdAt: day(1) },
      { id: 2, budgetPlanId: 1, categoryId: 1, lineType: 'Expense', bucket: 'Buffer', cadence: 'Monthly', amount: 900, monthlyEquivalent: 900, isStressFactor: false, sortOrder: 2, createdAt: day(1) },
      { id: 3, budgetPlanId: 1, categoryId: 4, lineType: 'Income', bucket: 'Core', cadence: 'Monthly', amount: 12345.67, monthlyEquivalent: 12345.67, isStressFactor: false, sortOrder: 3, createdAt: day(1) },
    ],
  },
];

export const moneyMap: MoneyMap = {
  planName: 'Household plan', from: `${month}-01T00:00:00`, to: day(20), months: 3, plannedIncomeMonthly: 12345.67, actualIncomeMonthly: 12001.5,
  lines: [
    { categoryId: 2, name: 'Rent & Mortgage Payment (Primary Residence)', bucket: 'Core', cadence: 'Monthly', planMonthly: 2450, actualMonthly: 2450, topPayees: ['Rent'], notes: null },
    { categoryId: 1, name: 'Groceries', bucket: 'Buffer', cadence: 'Monthly', planMonthly: 900, actualMonthly: 1234.56, topPayees: ['WHOLE FOODS MARKET #10234 KATY TX'], notes: null },
  ],
  upcoming: [{ date: day(28), label: 'Car insurance (6-month premium)', amount: 1450.25, kind: 'Bill' }],
};

export const budgetAnalysis: BudgetAnalysis = {
  planMonth: {
    plan: { id: 1, name: 'Household plan', planMonth: `${month}-01` },
    analyzedMonth: `${month}-01`,
    pacing: { daysElapsed: 20, daysInMonth: 30, daysPct: 66.7, spentPct: 80, projectedEnd: 4500, pacingDelta: 300, status: 'Behind', plannedExpenses: 3350, actualExpenses: 3684.56, remaining: -334.56, perDiemToStay: 0 },
    byCategory: [
      { categoryId: 2, planned: 2450, actual: 2450, overBy: 0 },
      { categoryId: 1, planned: 900, actual: 1234.56, overBy: 334.56 },
      { categoryId: null, planned: 0, actual: 18.4, overBy: 18.4 },
    ],
    byBucket: [{ bucket: 'Core', planned: 2450, actual: 2450 }, { bucket: 'Buffer', planned: 900, actual: 1252.96 }],
    income: 12345.67, expenses: 3702.96,
  },
  windowSpend: [{ categoryId: 1, amount: 1234.56 }, { categoryId: 2, amount: 2450 }],
  monthlyTrend: [{ categoryId: 1, month: `${month}-01`, income: 0, expenses: 1234.56 }],
};

export const cashFlow: CashFlowReport = {
  periods: [{ start: `${month}-01T00:00:00`, moneyIn: 12345.67, moneyOut: 3702.96, net: 8642.71, balance: 24310.55 }],
  totals: { moneyIn: 12345.67, moneyOut: 3702.96, net: 8642.71 },
  previousTotals: { moneyIn: 12001.5, moneyOut: 4100, net: 7901.5 },
  pending: { moneyIn: 0, moneyOut: 18.4, net: -18.4 },
  incomeSources: [{ source: 'ACME CORP PAYROLL DIRECT DEP', amount: 12345.67 }],
  spendByCategory: [{ categoryId: 2, amount: 2450 }, { categoryId: 1, amount: 1234.56 }],
  currentBalance: 24310.55,
};

export const plaidConnections = [CHASE, ALLY];
export const webull = webullView();
export const webullHoldings = holdingsView();
export const webullTrades = tradesView();
