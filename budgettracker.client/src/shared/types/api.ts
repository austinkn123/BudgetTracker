export interface User {
  id: number;
  email: string;
  createdAt: string;
}

export interface Category {
  id: number;
  userId: number;
  name: string;
  categoryType: string;
  /** Plaid taxonomy value (personal_finance_category.primary) this category claims, if any. */
  plaidCategoryPrimary?: string | null;
}

export interface Transaction {
  id: number;
  accountId: number;
  transactionType: string;
  /** Null for uncategorised rows — every imported transaction starts this way. */
  categoryId: number | null;
  amount: number;
  occurredAt: string;
  payee?: string;
  transferAccountId?: number;
  notes?: string;
  createdAt: string;
  plaidTransactionId?: string | null;
  plaidAccountId?: string | null;
  /** Plaid's suggested category. Retained even after the user overrides categoryId. */
  plaidCategoryPrimary?: string | null;
  isImported?: boolean;
  isPending?: boolean;
}

export interface PlaidLinkedAccountView {
  plaidAccountId: string;
  name: string;
  mask?: string | null;
  accountType: string;
}

export interface PlaidConnectionView {
  plaidItemId: number;
  institutionName: string;
  lastSyncedAt?: string | null;
  accounts: PlaidLinkedAccountView[];
}

export interface PlaidSyncSummary {
  inserted: number;
  updated: number;
  removed: number;
  syncedAt: string;
}

export interface PlaidLinkTokenResponse {
  linkToken: string;
  expiration: string;
}

export interface BudgetPlanEntry {
  id: number;
  budgetPlanId: number;
  categoryId?: number | null;
  lineType: 'Income' | 'Expense';
  bucket: 'Core' | 'Buffer';
  cadence: 'Monthly' | 'Annual';
  amount: number;
  monthlyEquivalent: number;
  isStressFactor: boolean;
  notes?: string | null;
  sortOrder: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BudgetPlan {
  id: number;
  userId: number;
  name: string;
  planMonth: string;
  netIncomeMonthly: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  entries: BudgetPlanEntry[];
}

// ---- Budget analysis: mirrors BudgetTracker.Domain/Models/Analysis ----
// Complete, unordered, unlabelled numbers. Nothing here is truncated or formatted —
// the dashboard's selection helpers (features/dashboard/utils) choose what to show.
// A null categoryId means "uncategorized".

export interface AnalyzedPlan {
  id: number;
  name: string;
  planMonth: string;
}

export type PacingStatus = 'Ahead' | 'OnTrack' | 'Behind';

export interface PeriodPacing {
  daysElapsed: number;
  daysInMonth: number;
  daysPct: number;
  spentPct: number;
  projectedEnd: number;
  pacingDelta: number;
  status: PacingStatus;
  plannedExpenses: number;
  actualExpenses: number;
  remaining: number;
  perDiemToStay: number;
}

export interface CategoryPerformance {
  categoryId: number | null;
  planned: number;
  actual: number;
  /** Positive when actual spend has exceeded the planned amount. */
  overBy: number;
}

export interface BucketPerformance {
  bucket: 'Core' | 'Buffer';
  planned: number;
  actual: number;
}

export interface PlanPerformance {
  plan: AnalyzedPlan;
  /**
   * The month these figures describe. Differs from `plan.planMonth` (the month the plan took
   * effect) whenever a plan has rolled forward past its own month.
   */
  analyzedMonth: string;
  pacing: PeriodPacing;
  byCategory: CategoryPerformance[];
  byBucket: BucketPerformance[];
  income: number;
  expenses: number;
}

export interface CategorySpend {
  categoryId: number | null;
  amount: number;
}

export interface CategoryMonthSpend {
  categoryId: number | null;
  /** ISO date at the first of the month. */
  month: string;
  income: number;
  expenses: number;
}

export interface BudgetAnalysis {
  /** Null when the user has no active plan. */
  planMonth: PlanPerformance | null;
  windowSpend: CategorySpend[];
  monthlyTrend: CategoryMonthSpend[];
}
