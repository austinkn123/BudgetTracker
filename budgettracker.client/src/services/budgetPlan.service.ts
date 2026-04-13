import api from '../lib/api';
import type { BudgetPlan } from '../types/api';

export const budgetPlanService = {
  getById: async (id: number): Promise<BudgetPlan> => {
    const response = await api.get<BudgetPlan>(`/budget-plans/${id}`);
    return response.data;
  },

  getCurrentUserBudgetPlans: async (): Promise<BudgetPlan[]> => {
    const response = await api.get<BudgetPlan[]>('/budget-plans');
    return response.data;
  },

  create: async (budgetPlan: Omit<BudgetPlan, 'id'>): Promise<BudgetPlan> => {
    const response = await api.post<BudgetPlan>('/budget-plans', budgetPlan);
    return response.data;
  },

  update: async (id: number, budgetPlan: BudgetPlan): Promise<BudgetPlan> => {
    const response = await api.put<BudgetPlan>(`/budget-plans/${id}`, budgetPlan);
    return response.data;
  },

  delete: async (id: number): Promise<void> => {
    await api.delete(`/budget-plans/${id}`);
  },
};