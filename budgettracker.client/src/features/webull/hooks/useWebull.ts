import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { webullService } from '../../../shared/services/webull.service';
import type {
  WebullConnectionView,
  WebullHoldingsView,
  WebullRange,
  WebullTradeHistoryStatus,
  WebullTradesView,
} from '../../../shared/types/api';

/**
 * Brokerage balances live under their own key. Nothing here touches transactions, budget-analysis
 * or cash-flow: Webull is kept out of the budget figures entirely.
 */
export const WEBULL_QUERY_KEY = ['webull'] as const;

/**
 * Deliberately not under ['webull', …]: storeView's cancelQueries matches by prefix, and would
 * cancel a holdings GET in flight every time a refresh or verify lands.
 */
export const WEBULL_HOLDINGS_QUERY_KEY = ['webull-holdings'] as const;

/** Prefix of every range's trades. Outside ['webull'] for the same cancel-by-prefix reason. */
export const WEBULL_TRADES_QUERY_KEY = ['webull-trades'] as const;

const POLL_INTERVAL_MS = 5_000;
/** Polling outlives the approval window by this much, then stops even if the state never moves. */
const POLL_CAP_AFTER_EXPIRY_MS = 10_000;

/**
 * Store a view that came from refresh/verify rather than the GET. Cancelling first stops an older
 * GET that is still in flight from landing afterwards and overwriting the newer view.
 */
const storeView = async (queryClient: QueryClient, view: WebullConnectionView) => {
  await queryClient.cancelQueries({ queryKey: WEBULL_QUERY_KEY });
  queryClient.setQueryData(WEBULL_QUERY_KEY, view);
};

/** Last known Webull state, shared by the Dashboard card and the Settings section. */
export const useWebull = () =>
  useQuery<WebullConnectionView, Error>({
    queryKey: WEBULL_QUERY_KEY,
    queryFn: webullService.get,
  });

/** Last known positions per account, for the Investments page. */
export const useWebullHoldings = () =>
  useQuery<WebullHoldingsView, Error>({
    queryKey: WEBULL_HOLDINGS_QUERY_KEY,
    queryFn: webullService.getHoldings,
  });

/** Saved closed trades and their summary for one range. Never calls Webull. */
export const useWebullTrades = (range: WebullRange) =>
  useQuery<WebullTradesView, Error>({
    queryKey: [...WEBULL_TRADES_QUERY_KEY, range],
    queryFn: () => webullService.getTrades(range),
  });

/** Whether another sync would pull more history: unfinished, and nothing standing in the way. */
const shouldSyncAgain = (status: WebullTradeHistoryStatus) =>
  !status.isComplete && status.lastError === null && status.state === 'Connected';

/** The sync in flight per client, so a tab reopened mid-call joins it instead of overlapping it. */
const tradeSyncsInFlight = new WeakMap<QueryClient, Promise<WebullTradeHistoryStatus>>();

const syncTradesOnce = (queryClient: QueryClient) => {
  let sync = tradeSyncsInFlight.get(queryClient);
  if (!sync) {
    sync = webullService.syncTrades().finally(() => tradeSyncsInFlight.delete(queryClient));
    tradeSyncsInFlight.set(queryClient, sync);
  }
  return sync;
};

/**
 * Backfills trade history while the Trades tab is open, and says whether it still is.
 *
 * The first sync always goes out on open, whatever the saved status says: a stored lastError or
 * state is from the last attempt, and only a new one can clear it. After that, syncs run one after
 * another (each can take ~15 s server-side), never overlapping, while the latest says there is
 * more to pull. Each one re-reads the trades, and only the trades. Stops on unmount; a failure is
 * never retried (the next open tries again).
 */
export const useWebullTradeSync = () => {
  const queryClient = useQueryClient();
  const hasStarted = useRef(false);
  const isMounted = useRef(false);
  const [isSyncing, setIsSyncing] = useState(true);

  useEffect(() => {
    isMounted.current = true;
    // StrictMode's dev-only remount keeps the refs: one chain per mount, still live after it.
    if (!hasStarted.current) {
      hasStarted.current = true;
      void (async () => {
        try {
          while (isMounted.current) {
            const status = await syncTradesOnce(queryClient);
            if (!isMounted.current) return;
            // Awaited so the re-read lands first: the page never shows the old status as final.
            await queryClient.invalidateQueries({ queryKey: WEBULL_TRADES_QUERY_KEY });
            if (!shouldSyncAgain(status)) return;
          }
        } catch {
          // Not retried; the trades already on screen stay up.
        } finally {
          setIsSyncing(false);
        }
      })();
    }
    return () => {
      isMounted.current = false;
    };
  }, [queryClient]);

  return isSyncing;
};

/**
 * One silent `refresh()` per mount of the host page, modelled on useSyncOnOpen. The server decides
 * whether the balance is stale (and does nothing when Webull isn't configured); a failure leaves
 * whatever the GET returned on screen and never reaches the rest of the page. A refresh also
 * updates positions server-side, so holdings are re-read (only fetched where they're shown).
 */
export const useWebullRefreshOnOpen = () => {
  const queryClient = useQueryClient();
  const hasFired = useRef(false);

  const { mutate } = useMutation({
    mutationFn: webullService.refresh,
    retry: false,
    onSuccess: async (view) => {
      await storeView(queryClient, view);
      void queryClient.invalidateQueries({ queryKey: WEBULL_HOLDINGS_QUERY_KEY });
    },
  });

  useEffect(() => {
    // The ref survives StrictMode's dev-only double effect, so this is one request per mount.
    if (hasFired.current) return;
    hasFired.current = true;
    mutate();
  }, [mutate]);
};

/**
 * Asks Webull to text the approval code. Each call sends an SMS, so it never retries and callers
 * disable it while pending.
 */
export const useVerifyWebull = () => {
  const queryClient = useQueryClient();
  return useMutation<WebullConnectionView, Error>({
    mutationFn: webullService.verify,
    retry: false,
    onSuccess: (view) => storeView(queryClient, view),
  });
};

/**
 * While Webull waits for SMS approval, poll `refresh()` every 5 s so the approval shows up without
 * a reload. Stops as soon as the state changes, and in any case once the approval window (plus a
 * margin) has passed. Polls are chained, not interval-fired, so a slow response never overlaps.
 *
 * At the cap it re-reads the GET (no Webull call): if the polls were failing, or this clock runs
 * ahead of the server's, the screen would otherwise sit on "Waiting" with no action. The server
 * reports ApprovalExpired, with Verify offered again, once its own window has passed.
 */
export const useApprovalPolling = (view: WebullConnectionView | undefined) => {
  const queryClient = useQueryClient();
  const isWaiting = view?.state === 'WaitingForApproval';
  const expiresAt = view?.approvalExpiresAt ?? null;

  useEffect(() => {
    if (!isWaiting) return;

    // ponytail: no cap if the server ever omits approvalExpiresAt while waiting; the contract says it won't.
    const stopAt = expiresAt ? Date.parse(expiresAt) + POLL_CAP_AFTER_EXPIRY_MS : Infinity;
    let stopped = false;
    let timer: ReturnType<typeof setTimeout>;

    const poll = async () => {
      if (Date.now() > stopAt) {
        // ponytail: one re-read; a clock more than the margin ahead of the server's can still
        // read "Waiting" here. Keep re-reading the GET on this cadence if that ever shows up.
        void queryClient.invalidateQueries({ queryKey: WEBULL_QUERY_KEY });
        return;
      }
      try {
        const next = await webullService.refresh();
        if (!stopped) await storeView(queryClient, next);
      } catch {
        // A failed poll is simply retried on the next tick; the cap above still bounds it.
      }
      if (!stopped) timer = setTimeout(poll, POLL_INTERVAL_MS);
    };

    timer = setTimeout(poll, POLL_INTERVAL_MS);
    return () => {
      stopped = true;
      clearTimeout(timer);
    };
  }, [isWaiting, expiresAt, queryClient]);
};
