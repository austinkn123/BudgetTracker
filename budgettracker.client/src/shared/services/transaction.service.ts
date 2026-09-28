import api from '../api';
import type { Transaction } from '../types/api';

/** Mirrors the server's TransactionFilter. Omitted fields mean "no constraint". */
export type TransactionQuery = {
  from?: string;
  to?: string;
  uncategorized?: boolean;
  isImported?: boolean;
  isPending?: boolean;
  search?: string;
};

const toParams = (query: TransactionQuery): Record<string, string> => {
  const params: Record<string, string> = {};
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') params[key] = String(value);
  }
  return params;
};

export const transactionService = {
  getCurrentUserTransactions: async (): Promise<Transaction[]> => {
    const response = await api.get<Transaction[]>('/transactions');
    return response.data;
  },

  /** Server-side filtered read. With an empty query this is the same as the unfiltered list. */
  getFiltered: async (query: TransactionQuery): Promise<Transaction[]> => {
    const response = await api.get<Transaction[]>('/transactions', { params: toParams(query) });
    return response.data;
  },

  /** Set (or clear, with a null categoryId) the category on many rows in one request. */
  setCategoryBulk: async (ids: number[], categoryId: number | null): Promise<number> => {
    const response = await api.patch<{ updated: number }>('/transactions/category', { ids, categoryId });
    return response.data.updated;
  },

  /** Set or clear the note on one transaction. */
  setNotes: async (id: number, notes: string | null): Promise<void> => {
    await api.patch('/transactions/notes', { id, notes });
  },
};
