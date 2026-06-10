import api from '../api';
import type {
  PlaidConnectionView,
  PlaidLinkTokenResponse,
  PlaidSyncSummary,
} from '../types/api';

/**
 * Thin axios wrapper for the /api/plaid endpoints. All requests are authenticated via
 * the shared axios interceptor (Cognito ID token).
 */
export const plaidService = {
  /** Lazily fetch a Plaid Link token. Called on "Connect" click, not on page mount. */
  createLinkToken: async (): Promise<PlaidLinkTokenResponse> => {
    const response = await api.post<PlaidLinkTokenResponse>('/plaid/link-token');
    return response.data;
  },

  /** Exchange the public_token from a successful Link flow and trigger the initial sync. */
  exchangePublicToken: async (publicToken: string): Promise<PlaidSyncSummary> => {
    const response = await api.post<PlaidSyncSummary>('/plaid/exchange-token', { publicToken });
    return response.data;
  },

  /** Manual "Refresh" — re-pull transactions, dedupe by Plaid.transaction_id. */
  sync: async (): Promise<PlaidSyncSummary> => {
    const response = await api.post<PlaidSyncSummary>('/plaid/sync');
    return response.data;
  },

  /** Returns the current active PlaidItem metadata, or null when nothing is linked. */
  getConnection: async (): Promise<PlaidConnectionView | null> => {
    try {
      const response = await api.get<PlaidConnectionView>('/plaid/connection');
      return response.data;
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 404) return null;
      throw err;
    }
  },

  /** Revoke the active connection server-side and soft-delete the PlaidItem row. */
  disconnect: async (): Promise<void> => {
    await api.delete('/plaid/connection');
  },
};
