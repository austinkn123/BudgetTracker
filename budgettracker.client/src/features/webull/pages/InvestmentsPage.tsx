import { Navigate } from 'react-router-dom';
import { Alert, Card, PageHeader, Skeleton, Table, Tabs } from '../../../shared/components/ui';
import type { TableColumn } from '../../../shared/components/ui';
import type { WebullAccountHoldings, WebullHolding } from '../../../shared/types/api';
import {
  inCurrency,
  percentPrecise,
  quantity,
  unitPrice,
  withSign,
} from '../../../shared/utils/format';
import { chartPalette } from '../../dashboard/utils/chartTheme';
import {
  Direction,
  MISSING,
  Stat,
  SymbolCell,
  overlineClass,
} from '../components/Figures';
import TradesTab from '../components/TradesTab';
import { WebullAlerts, WebullAsOf, WebullDataNote } from '../components/WebullNotices';
import { useWebullHoldings, useWebullRefreshOnOpen } from '../hooks/useWebull';
import { useTabParam } from '../../../shared/hooks/useTabParam';

/** The US market's own calendar day. A pull on its weekend has no trading day to report. */
const marketWeekday = new Intl.DateTimeFormat('en-US', {
  timeZone: 'America/New_York',
  weekday: 'short',
});
// ponytail: weekends only; a 0 on a market holiday still reads as a plain $0.00. A holiday
// calendar (or the server saying so) is the upgrade if that ever confuses.
const isMarketWeekend = (iso: string | null) =>
  iso !== null && ['Sat', 'Sun'].includes(marketWeekday.format(new Date(iso)));

const moneyIn = (currency: string) => (amount: number | null) =>
  amount === null ? MISSING : inCurrency(amount, currency);

const signedMoneyIn = (currency: string) => (amount: number | null) =>
  amount === null ? MISSING : withSign(amount, (v) => inCurrency(v, currency));

/** Server order and server figures throughout: the page formats, it never computes. */
const columnsFor = (currency: string): TableColumn<WebullHolding>[] => {
  const money = moneyIn(currency);
  const signedMoney = signedMoneyIn(currency);
  return [
    {
      key: 'symbol',
      header: 'Symbol',
      render: (p) => <SymbolCell symbol={p.symbol} instrumentType={p.instrumentType} />,
    },
    {
      key: 'quantity',
      header: 'Quantity',
      align: 'right',
      render: (p) => (p.quantity === null ? MISSING : quantity(p.quantity)),
    },
    {
      key: 'averageCost',
      header: 'Avg cost/share',
      align: 'right',
      render: (p) => (p.averageCost === null ? MISSING : unitPrice(p.averageCost, currency)),
    },
    { key: 'totalCost', header: 'Total cost', align: 'right', render: (p) => money(p.totalCost) },
    {
      key: 'marketValue',
      header: 'Market value',
      align: 'right',
      render: (p) => money(p.marketValue),
    },
    {
      key: 'openProfitLoss',
      header: 'Open gain/loss',
      align: 'right',
      render: (p) => (
        <>
          <span className="block">{signedMoney(p.openProfitLoss)}</span>
          <span className="block text-xs text-ink-muted">
            <span>
              {p.openProfitLossPercent === null
                ? MISSING
                : withSign(p.openProfitLossPercent, percentPrecise)}
            </span>
            <Direction value={p.openProfitLoss ?? p.openProfitLossPercent} />
          </span>
        </>
      ),
    },
    {
      key: 'percentOfHoldings',
      header: '% of holdings',
      align: 'right',
      render: (p) => (p.percentOfHoldings === null ? MISSING : percentPrecise(p.percentOfHoldings)),
    },
  ];
};

/**
 * Where the holdings sit, straight from each position's `percentOfHoldings` (no client maths).
 * The bar is decoration; the labelled list beside it, and the table's own column, carry the
 * figures for assistive tech.
 */
const Allocation = ({
  label,
  positions,
  cash,
}: {
  label: string;
  positions: WebullHolding[];
  cash: string | null;
}) => (
  <div className="flex flex-col gap-3">
    <p className={overlineClass}>Allocation</p>
    <div
      aria-hidden
      className="flex h-2.5 origin-left animate-grow-x gap-0.5 overflow-hidden rounded-full"
    >
      {positions.map(
        (p, index) =>
          p.percentOfHoldings !== null && (
            <span
              key={`${index}:${p.symbol}`}
              className="h-full min-w-0"
              // Data-driven width and a palette colour: runtime values no utility class can express.
              style={{
                width: `${p.percentOfHoldings}%`,
                backgroundColor: chartPalette[index % chartPalette.length],
              }}
            />
          ),
      )}
    </div>
    <ul aria-label={`${label} allocation`} className="flex flex-wrap gap-x-5 gap-y-1.5 text-sm">
      {positions.map((p, index) => (
        <li key={`${index}:${p.symbol}`} className="flex items-center gap-2">
          <span
            aria-hidden
            className="h-2.5 w-2.5 shrink-0 rounded-sm"
            // Same palette slot as the bar segment, so the two read as one.
            style={{
              backgroundColor:
                p.percentOfHoldings === null
                  ? undefined
                  : chartPalette[index % chartPalette.length],
            }}
          />
          <span className="font-medium text-ink">{p.symbol}</span>
          <span className="numeric text-ink-muted">
            {p.percentOfHoldings === null ? MISSING : percentPrecise(p.percentOfHoldings)}
          </span>
        </li>
      ))}
    </ul>
    {cash && (
      <p className="text-xs text-ink-muted">Cash of {cash} isn't counted in these percentages.</p>
    )}
  </div>
);

const AccountHoldings = ({
  account,
  asOf,
}: {
  account: WebullAccountHoldings;
  asOf: string | null;
}) => {
  const label = account.accountLabel ?? account.accountType;
  const money = moneyIn(account.currency);
  const signedMoney = signedMoneyIn(account.currency);
  // Webull reports a closed market's day as 0; on its own, "$0.00" reads as broken.
  const marketClosed = account.dayProfitLoss === 0 && isMarketWeekend(asOf);

  return (
    // A named region per account, so each one's figures and table are announced as a unit.
    <section aria-label={label} className="flex flex-col gap-4">
      <h2 className="text-[17px] font-semibold tracking-[-0.01em] text-ink">{label}</h2>

      <dl
        aria-label={`${label} summary`}
        className="grid grid-cols-2 gap-px overflow-hidden rounded-xl bg-border-subtle lg:grid-cols-4"
      >
        <Stat label="Total value" figure={money(account.totalValue)} />
        <Stat label="Cash" figure={money(account.cash)} />
        <Stat
          label="Day gain/loss"
          figure={signedMoney(account.dayProfitLoss)}
          detail={marketClosed ? 'Market closed' : <Direction value={account.dayProfitLoss} />}
        />
        <Stat
          label="Open gain/loss"
          figure={signedMoney(account.openProfitLoss)}
          detail={
            <>
              <span className="numeric">
                {account.openProfitLossPercent === null
                  ? MISSING
                  : withSign(account.openProfitLossPercent, percentPrecise)}
              </span>
              <Direction value={account.openProfitLoss} />
            </>
          }
        />
      </dl>

      <Card title="Holdings" contentClassName="flex flex-col gap-6">
        {account.positions.length > 0 ? (
          <>
            <Allocation
              label={label}
              positions={account.positions}
              cash={account.cash === null ? null : money(account.cash)}
            />
            <Table
              ariaLabel={`${label} holdings`}
              columns={columnsFor(account.currency)}
              rows={account.positions}
              // Symbols aren't unique within an account. The index is stable: rows keep server order.
              rowKey={(p, index) => `${index}:${p.symbol}`}
            />
          </>
        ) : (
          <p className="text-sm text-ink-muted">No positions</p>
        )}
      </Card>
    </section>
  );
};

const TABS = [
  { value: 'holdings', label: 'Holdings' },
  { value: 'trades', label: 'Trades' },
] as const;

/**
 * Read-only Webull investments: holdings per account, and closed trades. Kept apart from every
 * budget figure. Prices are Webull's as of the last refresh, never presented as live.
 */
const InvestmentsPage = () => {
  // Same silent refresh as the Dashboard; the server decides whether one is due.
  useWebullRefreshOnOpen();
  const { data: view, isPending } = useWebullHoldings();
  // Holdings is the bare /investments.
  const [tab, selectTab] = useTabParam(['holdings', 'trades']);

  if (isPending) {
    return (
      <div role="status" className="space-y-4">
        <span className="sr-only">Loading your holdings</span>
        <Skeleton className="h-8 w-48" />
        <Skeleton variant="rectangular" className="h-48 w-full" />
      </div>
    );
  }

  if (!view) {
    return (
      <Alert
        severity="error"
        title="Couldn't load your holdings"
        message="Reload the page to try again."
      />
    );
  }

  if (view.state === 'NotSetUp') return <Navigate to="/" replace />;

  return (
    <div className="stagger-in space-y-8">
      <PageHeader
        title="Investments"
        description={
          tab === 'holdings' &&
          view.asOf && (
            <WebullAsOf asOf={view.asOf} isStale={view.isStale} label="Prices from Webull as of" />
          )
        }
      />

      <Tabs value={tab} onChange={selectTab} options={TABS} ariaLabel="Investments">
        {tab === 'trades' ? (
          <TradesTab />
        ) : (
          <div className="stagger-in space-y-8">
            <WebullAlerts state={view.state} lastError={view.lastError} />
            <WebullDataNote lastError={view.lastError} hasData={view.accounts.length > 0} />

            {view.accounts.map((account) => (
              <AccountHoldings key={account.accountId} account={account} asOf={view.asOf} />
            ))}
          </div>
        )}
      </Tabs>
    </div>
  );
};

export default InvestmentsPage;
