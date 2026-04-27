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
