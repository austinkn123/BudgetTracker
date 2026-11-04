import { useMemo, useState } from 'react';
import { Alert, Card, PageHeader, Skeleton } from '../../../shared/components/ui';
import type { CashFlowReport } from '../../../shared/types/api';
import { cn } from '../../../shared/utils/cn';
import { currency, percent, signedAmount, signedCurrency } from '../../../shared/utils/format';
import { useCategories } from '../../categories/hooks/useCategories';
import { usePlaidConnections } from '../../linked-accounts/hooks/usePlaidConnections';
import { useSyncOnOpen } from '../../linked-accounts/hooks/useSyncOnOpen';
import StatStrip from '../../dashboard/components/StatStrip';
import type { Stat } from '../../dashboard/components/StatStrip';
import WhereItWent from '../../dashboard/components/WhereItWent';
import { semanticColors } from '../../dashboard/utils/chartTheme';
import { selectTopSpend } from '../../dashboard/utils/selectors';
import BalanceChart from '../components/BalanceChart';
import CashFlowChart from '../components/CashFlowChart';
import CashFlowToolbar from '../components/CashFlowToolbar';
import PeriodTransactions from '../components/PeriodTransactions';
import { useCashFlow } from '../hooks/useCashFlow';
import { useCashFlowControls } from '../hooks/useCashFlowControls';
import { percentChange, periodBounds, savingsRate, selectTopIncome } from '../utils/cashFlow';

const INCOME_SOURCES = 5;
const SPEND_CATEGORIES = 8;

/** "▲ 12% vs previous period", with the arrow spoken as a word. */
const Change = ({ value, amount }: { value: number; amount: string }) => (
  <span className="block">
    <span aria-hidden>{value >= 0 ? '▲' : '▼'} </span>
    <span className="sr-only">{value >= 0 ? 'Up' : 'Down'} </span>
    {amount} vs previous period
  </span>
);

const vsPrevious = (current: number, previous: number | undefined) => {
  if (previous === undefined) return null;
  const change = percentChange(current, previous);
  return change === null ? null : <Change value={change} amount={percent(Math.abs(change))} />;
};

// Signed from the value: pending refunds can outweigh pending spend. Cents kept — pending is small.
const pendingNote = (amount: number) =>
  amount !== 0 ? <span className="block">{signedAmount(amount, amount > 0)} pending</span> : null;

const buildStats = ({ totals, previousTotals: previous, pending }: CashFlowReport): Stat[] => {
  const rate = savingsRate(totals);
  const previousRate = previous ? savingsRate(previous) : null;
  const rateChange = rate !== null && previousRate !== null ? rate - previousRate : null;

  return [
    {
      label: 'Money in',
      value: totals.moneyIn,
      format: currency.format,
      tone: 'positive',
      detail: (
        <>
          {vsPrevious(totals.moneyIn, previous?.moneyIn)}
          {pendingNote(pending.moneyIn)}
        </>
      ),
    },
    {
      label: 'Money out',
      value: totals.moneyOut,
      format: currency.format,
      detail: (
        <>
          {vsPrevious(totals.moneyOut, previous?.moneyOut)}
          {pendingNote(pending.moneyOut)}
        </>
      ),
    },
    {
      label: 'Net',
      value: totals.net,
      format: signedCurrency,
      // Green when you kept money; a shortfall stays ink. Red is for over plan, not for spending.
      tone: totals.net >= 0 ? 'positive' : 'default',
      detail: vsPrevious(totals.net, previous?.net),
    },
    {
      label: 'Savings rate',
      value: rate ?? 0,
      format: rate === null ? () => '—' : percent,
      // A rate moves in points: 20% → 25% is up 5 points, not up 25%.
      detail:
        rateChange === null ? (
          rate === null ? 'Nothing came in' : null
        ) : (
          <Change value={rateChange} amount={`${Math.abs(Math.round(rateChange * 100))} pts`} />
        ),
    },
  ];
};

const LoadingState = () => (
  <div className="space-y-8" aria-busy>
    <div className="grid grid-cols-2 gap-px overflow-hidden rounded-xl bg-border-subtle lg:grid-cols-4">
      {Array.from({ length: 4 }, (_, i) => (
        <div key={i} className="space-y-2 bg-background px-5 py-4">
          <Skeleton width={64} height={10} />
          <Skeleton width={110} height={26} />
          <Skeleton width={90} height={10} />
        </div>
      ))}
    </div>
    <Card>
      <Skeleton width={180} height={18} />
      <Skeleton height={320} className="mt-4 rounded" />
    </Card>
  </div>
);

const CashFlowPage = () => {
  // Background "sync if stale" on open; silent, and refreshes the report when it lands.
  useSyncOnOpen();

  const controls = useCashFlowControls();
  const { data: report, isLoading, error, isPlaceholderData } = useCashFlow(controls.query);
  const { data: categories = [] } = useCategories();
  const { data: connections = [] } = usePlaidConnections();
  const [selectedStart, setSelectedStart] = useState<string | null>(null);

  const categoryNames = useMemo(
    () => new Map(categories.map((c) => [c.id, c.name])),
    [categories],
  );

  const stats = useMemo(() => (report ? buildStats(report) : []), [report]);
  const incomeRows = useMemo(
    () => selectTopIncome(report?.incomeSources ?? [], INCOME_SOURCES),
    [report?.incomeSources],
  );
  const spendRows = useMemo(
    () => selectTopSpend(report?.spendByCategory ?? [], categoryNames, SPEND_CATEGORIES),
    [report?.spendByCategory, categoryNames],
  );

  // A selection that no longer matches a bucket (new range or grouping) simply shows nothing.
  const selectedPeriod = report?.periods.find((p) => p.start === selectedStart) ?? null;

  return (
    <div className="stagger-in space-y-8">
      <PageHeader
        title="Cash Flow"
        description="Money in and out of your accounts. Moves between your own accounts are left out."
      />

      <CashFlowToolbar controls={controls} connections={connections} categories={categories} />

      {error && !report ? (
        <Alert
          severity="error"
          title="Couldn't load your cash flow"
          message="We couldn't reach the API. Check that the server and database are running."
        >
          {/* Raw errors stay behind a disclosure: useful when the API is down locally, but not copy. */}
          <details className="mt-2 text-xs">
            <summary className="cursor-pointer font-semibold">Technical details</summary>
            <span className="mt-1 block">{String(error)}</span>
          </details>
        </Alert>
      ) : isLoading || !report ? (
        <LoadingState />
      ) : (
        // Last report stays up, dimmed, while a changed control fetches the next one.
        <div className={cn('space-y-8 transition-opacity duration-120', isPlaceholderData && 'opacity-60')}>
          <StatStrip stats={stats} />

          <CashFlowChart
            periods={report.periods}
            grouping={controls.grouping}
            selected={selectedPeriod?.start ?? null}
            onSelect={setSelectedStart}
          />

          {selectedPeriod && (
            <PeriodTransactions
              key={selectedPeriod.start}
              period={selectedPeriod}
              grouping={controls.grouping}
              bounds={periodBounds(selectedPeriod.start, controls.grouping, controls.bounds)}
              plaidAccountId={controls.accountId}
              categoryId={controls.categoryId}
              search={controls.query.search ?? ''}
              categories={categories}
              onClear={() => setSelectedStart(null)}
            />
          )}

          {report.currentBalance !== null && (
            <BalanceChart
              periods={report.periods}
              grouping={controls.grouping}
              currentBalance={report.currentBalance}
            />
          )}

          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <WhereItWent
              rows={incomeRows}
              title="Where it came from"
              emptyTitle="No money came in during this range"
              emptyDetail="Paychecks and other deposits will break down here once they land."
              color={semanticColors.income}
            />
            {/* The ranked rows drop categories that refunds pushed negative; the total must not. */}
            <WhereItWent rows={spendRows} total={report.totals.moneyOut} />
          </div>
        </div>
      )}
    </div>
  );
};

export default CashFlowPage;
