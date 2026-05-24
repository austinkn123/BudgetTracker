// [SPIKE] BUD-4 — throwaway. Do not merge to main without replacing SpikePlaidAccessor.

import { useCallback, useEffect, useState } from 'react';
import { usePlaidLink as usePlaidLinkBase } from 'react-plaid-link';
import api from '../../../shared/api';

/**
 * Fetches a Plaid link token on mount, wires up the react-plaid-link handler,
 * and calls the exchange-token endpoint on a successful Link flow.
 *
 * Returns `ready` (whether Link is ready to open) and `open` (the function that
 * launches the Plaid Link iframe).
 */
export function usePlaidLink() {
  const [linkToken, setLinkToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isExchanging, setIsExchanging] = useState(false);

  // Fetch link token on mount
  useEffect(() => {
    let cancelled = false;

    api
      .post<{ linkToken: string }>('/plaid/link-token')
      .then((res) => {
        if (!cancelled) setLinkToken(res.data.linkToken);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(
            err?.response?.data ?? err?.message ?? 'Failed to fetch link token',
          );
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const onSuccess = useCallback(async (publicToken: string) => {
    setIsExchanging(true);
    setError(null);
    try {
      await api.post('/plaid/exchange-token', { publicToken });
    } catch (err: unknown) {
      const message =
        (err as { response?: { data?: string }; message?: string })?.response
          ?.data ??
        (err as { message?: string })?.message ??
        'Token exchange failed';
      setError(message);
    } finally {
      setIsExchanging(false);
    }
  }, []);

  const { open, ready } = usePlaidLinkBase({
    token: linkToken ?? '',
    onSuccess,
  });

  return { open, ready: ready && linkToken !== null, error, isExchanging };
}
