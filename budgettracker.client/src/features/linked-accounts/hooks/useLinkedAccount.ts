import { useCallback, useState } from 'react';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { plaidService } from '../../../shared/services/plaid.service';
import type { PlaidConnectionView, PlaidSyncSummary } from '../../../shared/types/api';

/**
 * Manages the full bank-link lifecycle: fetch link token on demand, exchange,
 * sync, disconnect. Surfaces a single ergonomic API for the LinkedAccountCard.
 */
export interface UseLinkedAccountResult {
  connection: PlaidConnectionView | null | undefined;
  isLoadingConnection: boolean;
  linkToken: string | null;
  isPreparingLink: boolean;
  isExchanging: boolean;
  isSyncing: boolean;
  isDisconnecting: boolean;
  errorMessage: string | null;
  lastSync: PlaidSyncSummary | null;
  /** Fetch a link token (only when the user clicks "Connect"). */
  prepareLink: () => Promise<string | null>;
  /** Clear the prepared link token after the Plaid Link UI finishes/exits. */
  clearLinkToken: () => void;
  /** Exchange the Plaid public_token and run the initial sync. */
  exchangePublicToken: (publicToken: string) => Promise<void>;
  /** Re-pull transactions from Plaid. */
  refresh: () => Promise<void>;
  /** Revoke the active connection. */
  disconnect: () => Promise<void>;
  /** Imperatively re-fetch the /connection endpoint. */
  refetchConnection: () => Promise<void>;
}

const CONNECTION_QUERY_KEY = ['plaid', 'connection'] as const;

const extractMessage = (err: unknown, fallback: string): string => {
  const responseError = (err as { response?: { data?: { error?: string } } })?.response?.data?.error;
  if (responseError) return responseError;
  const message = (err as { message?: string })?.message;
  return message ?? fallback;
};

export const useLinkedAccount = (): UseLinkedAccountResult => {
  const queryClient = useQueryClient();
  const [linkToken, setLinkToken] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [lastSync, setLastSync] = useState<PlaidSyncSummary | null>(null);

  const connectionQuery = useQuery({
    queryKey: CONNECTION_QUERY_KEY,
    queryFn: plaidService.getConnection,
    staleTime: 30_000,
  });

  const prepareLinkMutation = useMutation({
    mutationFn: plaidService.createLinkToken,
    onSuccess: (data) => {
      setLinkToken(data.linkToken);
      setErrorMessage(null);
    },
    onError: (err) => {
      setErrorMessage(extractMessage(err, 'Could not start bank link. Please try again.'));
    },
  });

  const exchangeMutation = useMutation({
    mutationFn: plaidService.exchangePublicToken,
    onSuccess: (summary) => {
      setLastSync(summary);
      setErrorMessage(null);
      void queryClient.invalidateQueries({ queryKey: CONNECTION_QUERY_KEY });
      void queryClient.invalidateQueries({ queryKey: ['transactions'] });
    },
    onError: (err) => {
      setErrorMessage(extractMessage(err, 'Could not finish linking your bank. Please try again.'));
    },
  });

  const syncMutation = useMutation({
    mutationFn: plaidService.sync,
    onSuccess: (summary) => {
      setLastSync(summary);
      setErrorMessage(null);
      void queryClient.invalidateQueries({ queryKey: CONNECTION_QUERY_KEY });
      void queryClient.invalidateQueries({ queryKey: ['transactions'] });
    },
    onError: (err) => {
      setErrorMessage(extractMessage(err, 'Could not refresh transactions. Please try again.'));
    },
  });

  const disconnectMutation = useMutation({
    mutationFn: plaidService.disconnect,
    onSuccess: () => {
      setLastSync(null);
      setErrorMessage(null);
      void queryClient.invalidateQueries({ queryKey: CONNECTION_QUERY_KEY });
    },
    onError: (err) => {
      setErrorMessage(extractMessage(err, 'Could not disconnect your bank. Please try again.'));
    },
  });

  const prepareLink = useCallback(async (): Promise<string | null> => {
    const result = await prepareLinkMutation.mutateAsync();
    return result.linkToken;
  }, [prepareLinkMutation]);

  const clearLinkToken = useCallback(() => setLinkToken(null), []);

  const exchangePublicToken = useCallback(async (publicToken: string) => {
    await exchangeMutation.mutateAsync(publicToken);
    setLinkToken(null);
  }, [exchangeMutation]);

  const refresh = useCallback(async () => {
    await syncMutation.mutateAsync();
  }, [syncMutation]);

  const disconnect = useCallback(async () => {
    await disconnectMutation.mutateAsync();
  }, [disconnectMutation]);

  const refetchConnection = useCallback(async () => {
    await connectionQuery.refetch();
  }, [connectionQuery]);

  return {
    connection: connectionQuery.data,
    isLoadingConnection: connectionQuery.isLoading,
    linkToken,
    isPreparingLink: prepareLinkMutation.isPending,
    isExchanging: exchangeMutation.isPending,
    isSyncing: syncMutation.isPending,
    isDisconnecting: disconnectMutation.isPending,
    errorMessage,
    lastSync,
    prepareLink,
    clearLinkToken,
    exchangePublicToken,
    refresh,
    disconnect,
    refetchConnection,
  };
};
