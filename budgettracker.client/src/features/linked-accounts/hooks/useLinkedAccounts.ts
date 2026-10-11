import { useCallback, useState } from 'react';
import { useQueryClient, useMutation } from '@tanstack/react-query';
import { plaidService } from '../../../shared/services/plaid.service';
import type { PlaidConnectionView, PlaidSyncSummary } from '../../../shared/types/api';
import { errorMessage as extractMessage } from '../../../shared/utils/errorMessage';
import { PLAID_CONNECTIONS_QUERY_KEY, usePlaidConnections } from './usePlaidConnections';

/**
 * A prepared Link session, saved so /plaid-oauth can resume it after a bank's OAuth redirect
 * (phones and in-app browsers can't open the OAuth popup, so the whole page navigates away).
 */
export interface SavedPlaidLink {
  linkToken: string;
  /** Set when the session is update mode for this PlaidItem; null when adding an institution. */
  reconnectItemId: number | null;
}

const PLAID_LINK_STORAGE_KEY = 'plaid-link';

export const savePlaidLink = (link: SavedPlaidLink) =>
  localStorage.setItem(PLAID_LINK_STORAGE_KEY, JSON.stringify(link));

export const loadPlaidLink = (): SavedPlaidLink | null => {
  const raw = localStorage.getItem(PLAID_LINK_STORAGE_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as SavedPlaidLink;
  } catch {
    return null;
  }
};

export const clearPlaidLink = () => localStorage.removeItem(PLAID_LINK_STORAGE_KEY);

/**
 * Manages the full bank-link lifecycle across every linked institution: fetch a link token on
 * demand (new institution or reconnect), finish Link, sync all, and disconnect one.
 */
export interface UseLinkedAccountsResult {
  connections: PlaidConnectionView[];
  isLoadingConnections: boolean;
  linkToken: string | null;
  isPreparingLink: boolean;
  isExchanging: boolean;
  isSyncing: boolean;
  /** The PlaidItem Link is open for in update mode (sign in again), or null. */
  reconnectingItemId: number | null;
  /** The PlaidItem currently being disconnected, or null. */
  disconnectingItemId: number | null;
  errorMessage: string | null;
  lastSync: PlaidSyncSummary | null;
  /** Fetch a link token (only when the user clicks "Add institution"). */
  prepareLink: () => Promise<void>;
  /** Fetch an update-mode link token so the person can sign in to this institution again. */
  prepareReconnect: (plaidItemId: number) => Promise<void>;
  /** Clear the prepared link token after the Plaid Link UI exits. */
  clearLinkToken: () => void;
  /**
   * Finish a successful Link session. Reconnect clears the needs-sign-in flag, then syncs (a sync
   * that still fails flags it again); a new institution exchanges the public_token and runs its
   * initial sync.
   */
  handleLinkSuccess: (publicToken: string) => Promise<void>;
  /** Re-pull transactions from every linked institution. */
  refresh: () => Promise<void>;
  /** Revoke a single institution. */
  disconnect: (plaidItemId: number) => Promise<void>;
  /** Dismiss the current error message. */
  clearError: () => void;
}

/** `resume` restores a saved Link session, for the OAuth redirect page. */
export const useLinkedAccounts = (resume?: SavedPlaidLink | null): UseLinkedAccountsResult => {
  const queryClient = useQueryClient();
  const [linkToken, setLinkToken] = useState<string | null>(resume?.linkToken ?? null);
  const [reconnectingItemId, setReconnectingItemId] = useState<number | null>(
    resume?.reconnectItemId ?? null,
  );
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
      savePlaidLink({ linkToken: data.linkToken, reconnectItemId: null });
      setErrorMessage(null);
    },
    onError: (err) => {
      setErrorMessage(extractMessage(err, "Couldn't start linking your bank. Please try again."));
    },
  });

  const prepareReconnectMutation = useMutation({
    mutationFn: plaidService.createReconnectLinkToken,
    onSuccess: (data, plaidItemId) => {
      setLinkToken(data.linkToken);
      savePlaidLink({ linkToken: data.linkToken, reconnectItemId: plaidItemId });
      setErrorMessage(null);
    },
    onError: (err) => {
      setReconnectingItemId(null);
      setErrorMessage(extractMessage(err, "Couldn't start reconnecting your bank. Please try again."));
    },
  });

  const reconnectedMutation = useMutation({
    mutationFn: plaidService.markReconnected,
    onError: (err) => {
      setErrorMessage(extractMessage(err, "Couldn't finish reconnecting your bank. Please try again."));
      // A 404 means the connection is gone or inactive; refetch so the list shows that.
      void queryClient.invalidateQueries({ queryKey: PLAID_CONNECTIONS_QUERY_KEY });
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
      // A sync that hits ITEM_LOGIN_REQUIRED flags the bank server-side; refetch so Reconnect shows.
      void queryClient.invalidateQueries({ queryKey: PLAID_CONNECTIONS_QUERY_KEY });
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

  const prepareReconnect = useCallback(async (plaidItemId: number) => {
    // Set before the request so the card shows progress and other actions lock immediately, and
    // so handleLinkSuccess (which usePlaidLink captures once, when the token arrives) sees it.
    setReconnectingItemId(plaidItemId);
    try {
      await prepareReconnectMutation.mutateAsync(plaidItemId);
    } catch {
      /* surfaced via errorMessage */
    }
  }, [prepareReconnectMutation]);

  const clearLinkToken = useCallback(() => {
    setLinkToken(null);
    setReconnectingItemId(null);
    clearPlaidLink();
  }, []);

  const clearError = useCallback(() => setErrorMessage(null), []);

  const refresh = useCallback(async () => {
    try {
      await syncMutation.mutateAsync();
    } catch {
      /* surfaced via errorMessage */
    }
  }, [syncMutation]);

  const handleLinkSuccess = useCallback(async (publicToken: string) => {
    setLinkToken(null);
    clearPlaidLink();
    if (reconnectingItemId !== null) {
      // Update mode: the access token is unchanged, so nothing is exchanged. Clear the flag first
      // (a sync never does), then sync: one that still fails flags the bank again.
      try {
        await reconnectedMutation.mutateAsync(reconnectingItemId);
        await refresh();
      } catch {
        /* surfaced via errorMessage */
      }
      setReconnectingItemId(null);
      return;
    }
    try {
      await exchangeMutation.mutateAsync(publicToken);
    } catch {
      /* surfaced via errorMessage */
    }
  }, [reconnectingItemId, reconnectedMutation, refresh, exchangeMutation]);

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
    reconnectingItemId,
    disconnectingItemId: disconnectMutation.isPending ? (disconnectMutation.variables ?? null) : null,
    errorMessage,
    lastSync,
    prepareLink,
    prepareReconnect,
    clearLinkToken,
    handleLinkSuccess,
    refresh,
    disconnect,
    clearError,
  };
};
