import api from '../lib/api';
import type { Category } from '../types/api';

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
