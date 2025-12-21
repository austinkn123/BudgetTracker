import api from '../lib/api';
import type { Expense } from '../types/api';

export const expenseService = {
  getById: async (id: number): Promise<Expense> => {
    const response = await api.get<Expense>(`/expenses/${id}`);
    return response.data;
  },
  
  getByUserId: async (userId: number): Promise<Expense[]> => {
    const response = await api.get<Expense[]>(`/expenses/user/${userId}`);
    return response.data;
  },
  
  create: async (expense: Omit<Expense, 'id'>): Promise<Expense> => {
    const response = await api.post<Expense>('/expenses', expense);
    return response.data;
  },
  
  update: async (id: number, expense: Expense): Promise<Expense> => {
    const response = await api.put<Expense>(`/expenses/${id}`, expense);
    return response.data;
  },
  
  delete: async (id: number): Promise<void> => {
    await api.delete(`/expenses/${id}`);
  },
};
