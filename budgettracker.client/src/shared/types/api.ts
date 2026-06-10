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
}

export interface Transaction {
  id: number;
  accountId: number;
  transactionType: string;
  categoryId: number;
  amount: number;
  occurredAt: string;
  payee?: string;
  transferAccountId?: number;
  notes?: string;
  createdAt: string;
  plaidTransactionId?: string | null;
  plaidAccountId?: string | null;
  isImported?: boolean;
  isPending?: boolean;
}

export interface PlaidLinkedAccountView {
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
