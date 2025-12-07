import api from '../lib/api';
import type { User, Category, Expense } from '../types/api';

// User services
export const userService = {
  getById: async (id: number): Promise<User> => {
    const response = await api.get<User>(`/users/${id}`);
    return response.data;
  },
  
  getByCognitoId: async (cognitoId: string): Promise<User> => {
    const response = await api.get<User>(`/users/cognito/${cognitoId}`);
    return response.data;
  },
  
  create: async (user: Omit<User, 'id'>): Promise<User> => {
    const response = await api.post<User>('/users', user);
    return response.data;
  },
  
  update: async (id: number, user: User): Promise<User> => {
    const response = await api.put<User>(`/users/${id}`, user);
    return response.data;
  },
  
  delete: async (id: number): Promise<void> => {
    await api.delete(`/users/${id}`);
  },
};

// Category services
export const categoryService = {
  getById: async (id: number): Promise<Category> => {
    const response = await api.get<Category>(`/categories/${id}`);
    return response.data;
  },
  
  getByUserId: async (userId: number): Promise<Category[]> => {
    const response = await api.get<Category[]>(`/categories/user/${userId}`);
    return response.data;
  },
  
  create: async (category: Omit<Category, 'id'>): Promise<Category> => {
    const response = await api.post<Category>('/categories', category);
    return response.data;
  },
  
  update: async (id: number, category: Category): Promise<Category> => {
    const response = await api.put<Category>(`/categories/${id}`, category);
    return response.data;
  },
  
  delete: async (id: number): Promise<void> => {
    await api.delete(`/categories/${id}`);
  },
};

// Expense services
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
