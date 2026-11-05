import api from '../api';
import type { Category, CategoryUsage } from '../types/api';

export const categoryService = {
  getById: async (id: number): Promise<Category> => {
    const response = await api.get<Category>(`/categories/${id}`);
    return response.data;
  },

  getCurrentUserCategories: async (): Promise<Category[]> => {
    const response = await api.get<Category[]>('/categories');
    return response.data;
  },

  /** Per-category transaction count (all time) and in/out sums from `since` (a `yyyy-MM-dd` date). */
  getUsage: async (since: string): Promise<CategoryUsage[]> => {
    const response = await api.get<CategoryUsage[]>('/categories/usage', { params: { since } });
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
