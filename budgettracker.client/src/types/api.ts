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
