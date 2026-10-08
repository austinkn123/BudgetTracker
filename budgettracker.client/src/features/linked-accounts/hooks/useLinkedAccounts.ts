import { useCallback, useState } from 'react';
import { useQueryClient, useMutation } from '@tanstack/react-query';
import { plaidService } from '../../../shared/services/plaid.service';
import type { PlaidConnectionView, PlaidSyncSummary } from '../../../shared/types/api';
import { PLAID_CONNECTIONS_QUERY_KEY, usePlaidConnections } from './usePlaidConnections';

/**
 * Manages the full bank-link lifecycle across every linked institution: fetch a link token on
 * demand, exchange (always adds an institution), sync all, and disconnect one.
 */
export interface UseLinkedAccountsResult {
  connections: PlaidConnectionView[];
  isLoadingConnections: boolean;
  linkToken: string | null;
  isPreparingLink: boolean;
  isExchanging: boolean;
  isSyncing: boolean;
  /** The PlaidItem currently being disconnected, or null. */
  disconnectingItemId: number | null;
  errorMessage: string | null;
  lastSync: PlaidSyncSummary | null;
  /** Fetch a link token (only when the user clicks "Add institution"). */
  prepareLink: () => Promise<void>;
  /** Clear the prepared link token after the Plaid Link UI finishes/exits. */
  clearLinkToken: () => void;
  /** Exchange the Plaid public_token, adding an institution and running its initial sync. */
  exchangePublicToken: (publicToken: string) => Promise<void>;
  /** Re-pull transactions from every linked institution. */
  refresh: () => Promise<void>;
  /** Revoke a single institution. */
  disconnect: (plaidItemId: number) => Promise<void>;
  /** Dismiss the current error message. */
  clearError: () => void;
}

const extractMessage = (err: unknown, fallback: string): string => {
  const responseError = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
  return responseError ?? fallback;
};

export const useLinkedAccounts = (): UseLinkedAccountsResult => {
  const queryClient = useQueryClient();
  const [linkToken, setLinkToken] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [lastSync, setLastSync] = useState<PlaidSyncSummary | null>(null);

  const connectionsQuery = usePlaidConnections();

  const invalidateAfterSync = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: PLAID_CONNECTIONS_QUERY_KEY });
    void queryClient.invalidateQueries({ queryKey: ['transactions'] });
    void queryClient.invalidateQueries({ queryKey: ['budget-analysis'] });
  }, [queryClient]);

  const prepareLinkMutation = useMutation({
    mutationFn: plaidService.createLinkToken,
    onSuccess: (data) => {
      setLinkToken(data.linkToken);
      setErrorMessage(null);
    },
    onError: (err) => {
      setErrorMessage(extractMessage(err, "Couldn't start linking your bank. Please try again."));
    },
  });

  const exchangeMutation = useMutation({
    mutationFn: plaidService.exchangePublicToken,
    onSuccess: (summary) => {
      setLastSync(summary);
      setErrorMessage(null);
      invalidateAfterSync();
    },
    onError: (err) => {
      setErrorMessage(extractMessage(err, "Couldn't finish linking your bank. Please try again."));
    },
  });

  const syncMutation = useMutation({
    mutationFn: () => plaidService.sync(),
    onSuccess: (summary) => {
      setLastSync(summary);
      setErrorMessage(null);
      invalidateAfterSync();
    },
    onError: (err) => {
      setErrorMessage(extractMessage(err, "Couldn't refresh your transactions. Please try again."));
    },
  });

  const disconnectMutation = useMutation({
    mutationFn: plaidService.disconnect,
    onSuccess: () => {
      setErrorMessage(null);
      void queryClient.invalidateQueries({ queryKey: PLAID_CONNECTIONS_QUERY_KEY });
    },
    onError: (err) => {
      setErrorMessage(extractMessage(err, "Couldn't disconnect your bank. Please try again."));
    },
  });

  // Errors are reported through errorMessage (set in each onError), so the imperative wrappers
  // swallow the rejection instead of leaking an unhandled promise into click handlers.
  const prepareLink = useCallback(async () => {
    try {
      await prepareLinkMutation.mutateAsync();
    } catch {
      /* surfaced via errorMessage */
    }
  }, [prepareLinkMutation]);

  const clearLinkToken = useCallback(() => setLinkToken(null), []);

  const clearError = useCallback(() => setErrorMessage(null), []);

  const exchangePublicToken = useCallback(async (publicToken: string) => {
    setLinkToken(null);
    try {
      await exchangeMutation.mutateAsync(publicToken);
    } catch {
      /* surfaced via errorMessage */
    }
  }, [exchangeMutation]);

  const refresh = useCallback(async () => {
    try {
      await syncMutation.mutateAsync();
    } catch {
      /* surfaced via errorMessage */
    }
  }, [syncMutation]);

  const disconnect = useCallback(async (plaidItemId: number) => {
    try {
      await disconnectMutation.mutateAsync(plaidItemId);
    } catch {
      /* surfaced via errorMessage */
    }
  }, [disconnectMutation]);

  return {
    connections: connectionsQuery.data ?? [],
    isLoadingConnections: connectionsQuery.isLoading,
    linkToken,
    isPreparingLink: prepareLinkMutation.isPending,
    isExchanging: exchangeMutation.isPending,
    isSyncing: syncMutation.isPending,
    disconnectingItemId: disconnectMutation.isPending ? (disconnectMutation.variables ?? null) : null,
    errorMessage,
    lastSync,
    prepareLink,
    clearLinkToken,
    exchangePublicToken,
    refresh,
    disconnect,
    clearError,
  };
};
