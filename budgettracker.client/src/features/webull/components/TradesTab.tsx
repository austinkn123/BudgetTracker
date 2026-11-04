import { useState } from 'react';
import { format, parseISO } from 'date-fns';
import { Alert, Card, Skeleton, Table, ToggleGroup } from '../../../shared/components/ui';
import type { TableColumn } from '../../../shared/components/ui';
import type {
  WebullClosedTrade,
  WebullRange,
  WebullTradeHistoryStatus,
  WebullTradesView,
} from '../../../shared/types/api';
import {
  inCurrency,
  localDateTime,
  percentPrecise,
  quantity,
  unitPrice,
  withSign,
} from '../../../shared/utils/format';
import { useWebullTrades, useWebullTradeSync } from '../hooks/useWebull';
import { Direction, MISSING, Stat, SymbolCell } from './Figures';
import { WebullAlerts, WebullDataNote } from './WebullNotices';

const RANGES = [
  { value: '1M', label: '1M' },
  { value: '3M', label: '3M' },
  { value: 'YTD', label: 'YTD' },
  { value: '1Y', label: '1Y' },
  { value: 'All', label: 'All' },
] as const;

const SUBJECT = 'trade history';

/** Orders carry no currency; the server's amounts are dollars. */
const USD = 'USD';
const signedUsd = (amount: number | null) =>
  amount === null ? MISSING : withSign(amount, (v) => inCurrency(v, USD));
const signedPercent = (value: number | null) =>
  value === null ? MISSING : withSign(value, percentPrecise);

/**
 * The server sends Eastern calendar dates (yyyy-MM-dd). parseISO reads a bare date as local
 * midnight, so the day printed is the day sent, in any zone; `new Date()` would read it as UTC
 * midnight and show the day before anywhere west of Greenwich.
 */
const calendarDate = (date: string) => format(parseISO(date), 'MMM d, yyyy');

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;

/** Server order and server figures throughout: the tab formats, it never computes. */
const COLUMNS: TableColumn<WebullClosedTrade>[] = [
  {
    key: 'closeDate',
    header: 'Closed',
    render: (t) => <span className="whitespace-nowrap">{calendarDate(t.closeDate)}</span>,
  },
  {
    key: 'symbol',
    header: 'Symbol',
    render: (t) => <SymbolCell symbol={t.symbol} instrumentType={t.instrumentType} />,
  },
  { key: 'quantity', header: 'Quantity', align: 'right', render: (t) => quantity(t.quantity) },
  {
    key: 'averageCost',
    header: 'Avg cost/share',
    align: 'right',
    render: (t) => unitPrice(t.averageCost, USD),
  },
  {
    key: 'salePrice',
    header: 'Sale price',
    align: 'right',
    render: (t) => unitPrice(t.salePrice, USD),
  },
  {
    key: 'realizedProfitLoss',
    header: 'Realized gain/loss',
    align: 'right',
    render: (t) => (
      <>
        <span className="block">{signedUsd(t.realizedProfitLoss)}</span>
        <span className="block text-xs text-ink-muted">
          <span>{signedPercent(t.realizedProfitLossPercent)}</span>
          <Direction value={t.realizedProfitLoss} />
        </span>
      </>
    ),
  },
];

/** "Jan 1, 2026 – Oct 24, 2026", or "Through Oct 24, 2026" for All. */
const period = ({ from, to }: WebullTradesView) =>
  from ? `${calendarDate(from)} – ${calendarDate(to)}` : `Through ${calendarDate(to)}`;

/**
 * Why the figures may not be final. While a sync runs, how far it has got. Once it has stopped
 * short (an error, Webull not connected, a failed call), that older trades are missing, so the
 * average costs, and the gains built on them, can still move.
 */
const historyNote = ({ isComplete, historyFrom }: WebullTradeHistoryStatus, isSyncing: boolean) => {
  if (isComplete) return null;
  const from = historyFrom && calendarDate(historyFrom);
  if (isSyncing) return from ? `Loading trade history back to ${from}` : 'Loading trade history…';
  return from
    ? `Trade history loaded back to ${from}. Older trades aren't included yet, so average costs and totals may change.`
    : 'Trade history is still incomplete, so average costs and totals may change.';
};

const Trades = ({ view, isSyncing }: { view: WebullTradesView; isSyncing: boolean }) => {
  const { history, summary, trades } = view;
  // An empty range has no total: "—", not a $0.00 that reads as breaking even.
  const total = trades.length > 0 ? summary.totalRealizedProfitLoss : null;
  const note = historyNote(history, isSyncing);

  return (
    <div className="flex flex-col gap-6">
      <WebullAlerts state={history.state} lastError={history.lastError} subject={SUBJECT} />

      {note && (
        // One live region for both notes, so the switch from loading to provisional is announced.
        <p
          role="status"
          className={isSyncing ? 'text-sm text-ink-muted' : 'text-sm font-medium text-warning-dark'}
        >
          {note}
        </p>
      )}

      <div className="flex flex-col gap-3">
        <dl
          aria-label="Trade summary"
          className="grid grid-cols-2 gap-px overflow-hidden rounded-xl bg-border-subtle lg:grid-cols-3"
        >
          <Stat
            label="Realized gain/loss"
            figure={signedUsd(total)}
            detail={<Direction value={total} />}
          />
          <Stat label="Wins" figure={String(summary.wins)} />
          <Stat label="Losses" figure={String(summary.losses)} />
          <Stat
            label="Win rate"
            figure={summary.winRate === null ? MISSING : percentPrecise(summary.winRate)}
          />
          <Stat
            label="Largest win"
            figure={signedUsd(summary.largestWin)}
            detail={<Direction value={summary.largestWin} />}
          />
          <Stat
            label="Largest loss"
            figure={signedUsd(summary.largestLoss)}
            detail={<Direction value={summary.largestLoss} />}
          />
        </dl>

        <ul className="flex flex-col gap-1 text-xs text-ink-muted">
          <li>Before fees · average-cost method · not tax figures</li>
          {summary.excludedIncompleteCost > 0 && (
            <li>
              {plural(summary.excludedIncompleteCost, 'trade', 'trades')} excluded — cost history
              incomplete
            </li>
          )}
          {summary.excludedUnsupported > 0 && (
            <li>
              {plural(summary.excludedUnsupported, 'option/short order', 'option/short orders')}{' '}
              not included
            </li>
          )}
        </ul>
        {/* hasData: an empty range has its own copy below, so only the Unreachable line applies. */}
        <WebullDataNote lastError={history.lastError} hasData subject={SUBJECT} />
      </div>

      <Card title="Closed trades">
        {trades.length > 0 ? (
          <Table
            ariaLabel="Closed trades"
            columns={COLUMNS}
            rows={trades}
            // Rows keep server order, so the index is stable alongside the order id.
            rowKey={(t, index) => `${index}:${t.accountId}:${t.orderId}`}
          />
        ) : (
          <p className="text-sm text-ink-muted">No closed trades in this range</p>
        )}
      </Card>
    </div>
  );
};

/**
 * Closed trades and realized gain/loss for a chosen range. Mounted only while the Trades tab is
 * open, which is what starts the history sync and what stops it.
 */
const TradesTab = () => {
  const [range, setRange] = useState<WebullRange>('YTD');
  const isSyncing = useWebullTradeSync();
  // No placeholder data on a range switch: another range's figures under this label would lie.
  const { data: view, isPending } = useWebullTrades(range);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <ToggleGroup<WebullRange>
          value={range}
          onChange={setRange}
          options={RANGES}
          ariaLabel="Trades date range"
          className="w-full sm:w-auto"
        />
        {view && (
          <p className="numeric text-xs text-ink-muted">
            {period(view)}
            {view.history.asOf && (
              <span className="block sm:inline">
                <span aria-hidden className="hidden sm:inline">
                  {' · '}
                </span>
                {`As of ${localDateTime(view.history.asOf)}`}
              </span>
            )}
          </p>
        )}
      </div>

      {isPending ? (
        <div role="status" className="space-y-4">
          <span className="sr-only">Loading your trades</span>
          <Skeleton variant="rectangular" className="h-36 w-full" />
          <Skeleton variant="rectangular" className="h-48 w-full" />
        </div>
      ) : view ? (
        <Trades view={view} isSyncing={isSyncing} />
      ) : (
        <Alert
          severity="error"
          title="Couldn't load your trades"
          message="Reload the page to try again."
        />
      )}
    </div>
  );
};

export default TradesTab;
