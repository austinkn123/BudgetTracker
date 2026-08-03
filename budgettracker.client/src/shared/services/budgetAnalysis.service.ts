import api from '../api';
import type { BudgetAnalysis } from '../types/api';

export const budgetAnalysisService = {
  /**
   * `from`/`to` are date-only strings (yyyy-MM-dd); `to` is inclusive of its whole day.
   * `trendMonths` controls the per-category trend window.
   */
  getAnalysis: async (from: string, to: string, trendMonths: number): Promise<BudgetAnalysis> => {
    const response = await api.get<BudgetAnalysis>('/budget-analysis', {
      params: { from, to, trendMonths },
    });
    return response.data;
  },
};
