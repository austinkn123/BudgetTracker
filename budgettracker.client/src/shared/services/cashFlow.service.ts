import api from '../api';
import type { CashFlowQuery, CashFlowReport } from '../types/api';
import { localToday } from '../utils/localToday';

export const cashFlowService = {
  /** Undefined fields are dropped by axios, so an omitted `from` means "All". */
  getReport: async (query: CashFlowQuery): Promise<CashFlowReport> => {
    const response = await api.get<CashFlowReport>('/cash-flow', { params: { ...query, today: localToday() } });
    return response.data;
  },
};
