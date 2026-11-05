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

/** GET /api/transactions/page. Newest first (occurredAt desc, id desc); `nextCursor` is opaque, null on the last page. */
export interface TransactionPage {
  items: Transaction[];
  nextCursor: string | null;
}

/** One account's signed net of still-pending rows (money in positive). */
export interface PendingByAccount {
  plaidAccountId: string;
  amount: number;
}

/** GET /api/transactions/summary. The small ledger-wide figures that used to need the whole ledger. */
export interface TransactionSummary {
  /** Rows that still need a category: no category and not a paired transfer (same rule as `needsCategory`). */
  uncategorizedCount: number;
  pendingByAccount: PendingByAccount[];
}

/** GET /api/categories/usage?since=. Only categories with at least one transaction appear. */
export interface CategoryUsage {
  categoryId: number;
  /** Every transaction filed here, any date. */
  transactionCount: number;
  /** Sum of positive amounts on or after `since`. */
  inflowSince: number;
  /** Sum of negative amounts on or after `since` (zero or below). */
  outflowSince: number;
}

export interface PlaidLinkedAccountView {
  plaidAccountId: string;
  name: string;
  mask?: string | null;
  accountType: string;
  /** Plaid's balance as of the last sync; for a card, the amount owed. Null until synced. */
  currentBalance?: number | null;
}

/** GET /api/money-map. Every actual is a monthly average over `months`; plan figures are monthly equivalents. */
export interface MoneyMap {
  /** The newest active plan; null when there is none. */
  planName: string | null;
  from: string;
  to: string;
  months: number;
  plannedIncomeMonthly: number;
  actualIncomeMonthly: number;
  /** Plan lines in plan order, then unplanned spend largest first. */
  lines: MoneyMapLine[];
  /** Lump-sum bills and extra paychecks in the next 12 months, soonest first. */
  upcoming: UpcomingItem[];
}

export interface MoneyMapLine {
  /** Null for uncategorized spend. */
  categoryId: number | null;
  name: string;
  /** Core = must-pay, Buffer = flexible; null when the line is not in the plan. */
  bucket: 'Core' | 'Buffer' | null;
  /** Annual lines are lump-sum bills set aside monthly. */
  cadence: 'Monthly' | 'Annual' | null;
  planMonthly: number;
  actualMonthly: number;
  /** Biggest payees by spend, at most three. */
  topPayees: string[];
  notes: string | null;
}

export interface UpcomingItem {
  date: string;
  label: string;
  amount: number;
  kind: 'Bill' | 'ExtraPaycheck';
}

export interface PlaidConnectionView {
  plaidItemId: number;
  institutionName: string;
  lastSyncedAt?: string | null;
  /** The bank wants the person to sign in again; syncing is paused until they reconnect. */
  needsReconnect: boolean;
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

export type WebullState =
  | 'NotSetUp'
  | 'NotConnected'
  | 'WaitingForApproval'
  | 'Connected'
  | 'ApprovalExpired'
  | 'NeedsVerification';

/** `TokenRejected` already surfaces as the `NeedsVerification` state; the client ignores it. */
export type WebullErrorKind = 'Unreachable' | 'KeyRejected' | 'TokenRejected';

export interface WebullBalance {
  accountId: string;
  accountLabel: string | null;
  accountType: string;
  totalNetLiquidationValue: number;
  totalCashBalance: number | null;
  /** ISO 4217 code; each account's amounts are formatted in its own currency. */
  currency: string;
}

/** GET /api/webull, POST /api/webull/refresh and POST /api/webull/verify. Read-only. */
export interface WebullConnectionView {
  state: WebullState;
  /** Empty when NotSetUp or nothing has been fetched yet. */
  balances: WebullBalance[];
  asOf: string | null;
  isStale: boolean;
  lastError: WebullErrorKind | null;
  /** Only set while WaitingForApproval. */
  approvalExpiresAt: string | null;
  canVerify: boolean;
}

/** One position. Computed values are null when Webull didn't supply them: render "—", never $0. Percents are percent units, 2 dp. */
export interface WebullHolding {
  symbol: string;
  instrumentType: string;
  quantity: number | null;
  averageCost: number | null;
  totalCost: number | null;
  marketValue: number | null;
  openProfitLoss: number | null;
  openProfitLossPercent: number | null;
  percentOfHoldings: number | null;
}

export interface WebullAccountHoldings {
  accountId: string;
  accountLabel: string | null;
  accountType: string;
  currency: string;
  totalValue: number;
  cash: number | null;
  dayProfitLoss: number | null;
  openProfitLoss: number | null;
  /** Open gain/loss as a return on cost, in percent units, 2 dp. */
  openProfitLossPercent: number | null;
  /** Server order: market value, largest first. */
  positions: WebullHolding[];
}

/** GET /api/webull/holdings. Never calls Webull; always 200. */
export interface WebullHoldingsView {
  state: WebullState;
  asOf: string | null;
  isStale: boolean;
  lastError: WebullErrorKind | null;
  accounts: WebullAccountHoldings[];
}

export type WebullRange = '1M' | '3M' | 'YTD' | '1Y' | 'All';

/** POST /api/webull/trades/sync. Always 200: a Webull failure comes back in `lastError`. */
export interface WebullTradeHistoryStatus {
  state: WebullState;
  /** UTC ISO instant of the last successful pull. */
  asOf: string | null;
  /** How far back the saved history reaches: yyyy-MM-dd, an Eastern calendar date. */
  historyFrom: string | null;
  isComplete: boolean;
  lastError: WebullErrorKind | null;
}

/** One closing order. Orders carry no currency, so amounts are USD. Percent in percent units. */
export interface WebullClosedTrade {
  accountId: string;
  orderId: string;
  /** yyyy-MM-dd, an Eastern calendar date. */
  closeDate: string;
  symbol: string;
  instrumentType: string;
  quantity: number;
  averageCost: number;
  salePrice: number;
  realizedProfitLoss: number;
  realizedProfitLossPercent: number | null;
}

export interface WebullTradeSummary {
  totalRealizedProfitLoss: number;
  wins: number;
  losses: number;
  /** Percent units, like every other Webull percent. */
  winRate: number | null;
  largestWin: number | null;
  largestLoss: number | null;
  /** Counts of trades left out of every figure above. */
  excludedIncompleteCost: number;
  excludedUnsupported: number;
}

/** GET /api/webull/trades?range=… Never calls Webull. */
export interface WebullTradesView {
  history: WebullTradeHistoryStatus;
  /** yyyy-MM-dd, Eastern. Null for All. */
  from: string | null;
  to: string;
  summary: WebullTradeSummary;
  /** Server order. */
  trades: WebullClosedTrade[];
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

// Cash flow — mirrors BudgetTracker.Domain.Models.CashFlow. Own-account transfers are excluded;
// pending rows count only toward `pending`. moneyOut is net of refunds.

export type CashFlowGrouping = 'Day' | 'Week' | 'Month' | 'Year';

export interface CashFlowTotals {
  moneyIn: number;
  moneyOut: number;
  net: number;
}

export interface CashFlowPeriod extends CashFlowTotals {
  /** ISO date: first day of the bucket. Weeks start on Sunday. */
  start: string;
  /** End-of-bucket balance; null whenever `CashFlowReport.currentBalance` is null. */
  balance: number | null;
}

export interface IncomeSource {
  source: string;
  amount: number;
}

export interface CashFlowReport {
  /** Oldest first, zero-filled across the window. */
  periods: CashFlowPeriod[];
  totals: CashFlowTotals;
  /** Null when no compare window was sent. */
  previousTotals: CashFlowTotals | null;
  pending: CashFlowTotals;
  /** Largest first. */
  incomeSources: IncomeSource[];
  spendByCategory: CategorySpend[];
  /** Checking + savings − card debt. Null when a category/search filter is set or no balance is synced. */
  currentBalance: number | null;
}

/** GET /api/cash-flow query string. Dates are yyyy-MM-dd; omit `from` for All. */
export interface CashFlowQuery {
  from?: string;
  to: string;
  groupBy: CashFlowGrouping;
  compareFrom?: string;
  compareTo?: string;
  plaidAccountId?: string;
  categoryId?: number;
  search?: string;
}
