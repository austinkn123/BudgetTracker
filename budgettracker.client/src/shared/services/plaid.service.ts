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

  /**
   * Link token for update mode on one institution, so the person can sign in to their bank
   * again. Finishing update mode needs no exchange: the existing access token keeps working.
   */
  createReconnectLinkToken: async (plaidItemId: number): Promise<PlaidLinkTokenResponse> => {
    const response = await api.post<PlaidLinkTokenResponse>(
      `/plaid/connections/${plaidItemId}/link-token`,
    );
    return response.data;
  },

  /**
   * Finish update mode: clear the connection's needs-sign-in flag. A sync does not clear it,
   * because a pending-expiration warning must survive syncs that still work.
   */
  markReconnected: async (plaidItemId: number): Promise<void> => {
    await api.post(`/plaid/connections/${plaidItemId}/reconnected`);
  },

  /**
   * Exchange the public_token from a successful Link flow and trigger the initial sync.
   * Always adds an institution; existing connections are left untouched.
   */
  exchangePublicToken: async (publicToken: string): Promise<PlaidSyncSummary> => {
    const response = await api.post<PlaidSyncSummary>('/plaid/exchange-token', { publicToken });
    return response.data;
  },

  /**
   * Re-pull transactions across every linked institution, deduped by Plaid.transaction_id.
   * With `staleAfterHours`, only institutions last synced longer ago than that are pulled;
   * without it, everything syncs (manual refresh).
   */
  sync: async (staleAfterHours?: number): Promise<PlaidSyncSummary> => {
    const response = await api.post<PlaidSyncSummary>(
      '/plaid/sync',
      undefined,
      staleAfterHours === undefined ? undefined : { params: { staleAfterHours } },
    );
    return response.data;
  },

  /** Every active linked institution. Empty array when nothing is linked. */
  getConnections: async (): Promise<PlaidConnectionView[]> => {
    const response = await api.get<PlaidConnectionView[]>('/plaid/connections');
    return response.data;
  },

  /** Revoke one institution server-side (Plaid /item/remove) and soft-delete its PlaidItem row. */
  disconnect: async (plaidItemId: number): Promise<void> => {
    await api.delete(`/plaid/connections/${plaidItemId}`);
  },
};
