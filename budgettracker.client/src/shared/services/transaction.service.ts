import api from "../api";
import type {
  Transaction,
  TransactionPage,
  TransactionSummary,
} from "../types/api";

/** Mirrors the server's TransactionFilter. Omitted fields mean "no constraint". */
export type TransactionQuery = {
  from?: string;
  to?: string;
  uncategorized?: boolean;
  isImported?: boolean;
  isPending?: boolean;
  search?: string;
  plaidAccountId?: string;
  categoryId?: number;
};

/** Paging for GET /transactions/page. The server defaults `limit` to 50 and caps it at 200. */
export type TransactionPageParams = {
  cursor?: string;
  limit?: number;
};

const toParams = (
  query: TransactionQuery & TransactionPageParams,
): Record<string, string> => {
  const params: Record<string, string> = {};
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== "") params[key] = String(value);
  }
  return params;
};

/** A header's file name, percent-decoded when it can be; a malformed escape keeps the raw text. */
const decodeFileName = (raw: string) => {
  try {
    return decodeURIComponent(raw);
  } catch {
    return raw;
  }
};

export const transactionService = {
  /**
   * Bounded, server-side filtered read (a month, the uncategorized backlog). The server refuses
   * this with no filter at all, so the open-ended ledger is only ever read through `getPage`.
   */
  getFiltered: async (query: TransactionQuery): Promise<Transaction[]> => {
    const params = toParams(query);
    if (Object.keys(params).length === 0) {
      throw new Error(
        "getFiltered needs at least one filter; page the full ledger with getPage.",
      );
    }
    const response = await api.get<Transaction[]>("/transactions", { params });
    return response.data;
  },

  /** One page of the ledger, newest first. Pass the previous page's `nextCursor` to continue. */
  getPage: async (
    query: TransactionQuery,
    page: TransactionPageParams = {},
  ): Promise<TransactionPage> => {
    const response = await api.get<TransactionPage>("/transactions/page", {
      params: toParams({ ...query, ...page }),
    });
    return response.data;
  },

  /**
   * Every row the feed shows for `query`, as a CSV file (BUD-25). Fetched through the API client
   * so the request carries the sign-in token; the file name comes from the server.
   */
  exportCsv: async (query: TransactionQuery): Promise<{ blob: Blob; fileName: string }> => {
    let response;
    try {
      response = await api.get<Blob>('/transactions/export', { params: toParams(query), responseType: 'blob' });
    } catch (err) {
      // A blob response carries the server's { error } as a Blob; read it so the caller can show the message.
      const data = (err as { response?: { data?: unknown } })?.response?.data;
      if (data instanceof Blob) {
        try {
          (err as { response: { data: unknown } }).response.data = JSON.parse(await data.text());
        } catch {
          // Not JSON: leave the blob, and the caller falls back to its generic message.
        }
      }
      throw err;
    }
    const disposition = String(response.headers?.['content-disposition'] ?? '');
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    return { blob: response.data, fileName: match ? decodeFileName(match[1]) : 'budgettracker-transactions.csv' };
  },

  /** Ledger-wide counts and pending sums, without fetching the ledger. */
  getSummary: async (): Promise<TransactionSummary> => {
    const response = await api.get<TransactionSummary>("/transactions/summary");
    return response.data;
  },

  /** Set (or clear, with a null categoryId) the category on many rows in one request. */
  setCategoryBulk: async (
    ids: number[],
    categoryId: number | null,
  ): Promise<number> => {
    const response = await api.patch<{ updated: number }>(
      "/transactions/category",
      { ids, categoryId },
    );
    return response.data.updated;
  },

  /** Set or clear the note on one transaction. */
  setNotes: async (id: number, notes: string | null): Promise<void> => {
    await api.patch("/transactions/notes", { id, notes });
  },
};
