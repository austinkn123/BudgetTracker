import api from '../api';
import type { CategoryRule, CategoryRuleCreated, CategoryRuleRequest } from '../types/api';

/** `/api/category-rules` (BUD-24). */
export const categoryRuleService = {
  getAll: async (): Promise<CategoryRule[]> => {
    const response = await api.get<CategoryRule[]>('/category-rules');
    return response.data;
  },

  /** How many uncategorized rows the rule would file if created now. */
  preview: async (rule: CategoryRuleRequest): Promise<number> => {
    const response = await api.post<{ count: number }>('/category-rules/preview', rule);
    return response.data.count;
  },

  create: async (rule: CategoryRuleRequest): Promise<CategoryRuleCreated> => {
    const response = await api.post<CategoryRuleCreated>('/category-rules', rule);
    return response.data;
  },

  update: async (id: number, rule: CategoryRuleRequest): Promise<void> => {
    await api.put(`/category-rules/${id}`, rule);
  },

  delete: async (id: number): Promise<void> => {
    await api.delete(`/category-rules/${id}`);
  },
};
