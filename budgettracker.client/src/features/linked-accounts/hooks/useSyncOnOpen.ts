import { useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { plaidService } from '../../../shared/services/plaid.service';
import { PLAID_CONNECTIONS_QUERY_KEY } from './usePlaidConnections';

/**
 * Only institutions last synced longer ago than this are pulled on open. This replaces the
 * background sweep, which can't run on AWS Lambda; Plaid webhooks cover the time in between.
 */
const STALE_AFTER_HOURS = 6;

/**
 * Fires one non-blocking "sync if stale" call when the host component mounts. It never blocks
 * rendering and never surfaces an error: a failed opportunistic sync gives the person nothing to
 * act on, and the manual Refresh in Settings reports failures properly.
 */
export const useSyncOnOpen = () => {
  const queryClient = useQueryClient();
  const hasFired = useRef(false);

  const { mutate } = useMutation({
    mutationFn: () => plaidService.sync(STALE_AFTER_HOURS),
    retry: false,
    onSuccess: (summary) => {
      void queryClient.invalidateQueries({ queryKey: PLAID_CONNECTIONS_QUERY_KEY });
      if (summary.inserted + summary.updated + summary.removed > 0) {
        void queryClient.invalidateQueries({ queryKey: ['transactions'] });
        void queryClient.invalidateQueries({ queryKey: ['budget-analysis'] });
      }
    },
    // Deliberately silent; see the doc comment above.
    onError: () => undefined,
  });

  useEffect(() => {
    // The ref survives StrictMode's dev-only double effect, so this is one request per mount.
    if (hasFired.current) return;
    hasFired.current = true;
    mutate();
  }, [mutate]);
};
