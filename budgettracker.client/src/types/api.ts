export interface User {
  id: number;
  cognitoUserId: string;
  email: string;
  createdAt: string;
}

export interface Category {
  id: number;
  userId: number;
  name: string;
}

export interface Expense {
  id: number;
  userId: number;
  categoryId: number;
  amount: number;
  date: string;
  merchant?: string;
  notes?: string;
  createdAt: string;
}
