import api from '../api';
import type {
  WebullConnectionView,
  WebullHoldingsView,
  WebullRange,
  WebullTradeHistoryStatus,
  WebullTradesView,
} from '../types/api';

/**
 * Thin axios wrapper for the read-only /api/webull endpoints. No request bodies: the app key,
 * secret and token live server-side and never pass through the browser.
 */
export const webullService = {
  /** Last known state and balances. Never calls Webull, so it is cheap to read on every open. */
  get: async (): Promise<WebullConnectionView> => {
    const response = await api.get<WebullConnectionView>('/webull');
    return response.data;
  },

  /**
   * Re-fetch balances if the server decides they are stale. Always 200: a Webull failure comes
   * back in `lastError`, not as a rejected request.
   */
  refresh: async (): Promise<WebullConnectionView> => {
    const response = await api.post<WebullConnectionView>('/webull/refresh');
    return response.data;
  },

  /**
   * Ask Webull to send the SMS approval code. Rejects with 409 `{ error }` when `canVerify` is
   * false or Webull isn't set up.
   */
  verify: async (): Promise<WebullConnectionView> => {
    const response = await api.post<WebullConnectionView>('/webull/verify');
    return response.data;
  },

  /** Last known positions per account. Never calls Webull; refresh() is what updates them. */
  getHoldings: async (): Promise<WebullHoldingsView> => {
    const response = await api.get<WebullHoldingsView>('/webull/holdings');
    return response.data;
  },

  /** Saved closed trades and their summary for a range. Never calls Webull. */
  getTrades: async (range: WebullRange): Promise<WebullTradesView> => {
    const response = await api.get<WebullTradesView>('/webull/trades', { params: { range } });
    return response.data;
  },

  /**
   * Pull the next slice of trade history from Webull (up to ~15 s server-side). Always 200: a
   * Webull failure comes back in `lastError`.
   */
  syncTrades: async (): Promise<WebullTradeHistoryStatus> => {
    const response = await api.post<WebullTradeHistoryStatus>('/webull/trades/sync');
    return response.data;
  },
};
