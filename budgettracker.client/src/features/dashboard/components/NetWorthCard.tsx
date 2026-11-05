import { useMemo } from 'react';
import { Card } from '../../../shared/components/ui';
import { currencyPrecise, localDateTime, signedCurrency } from '../../../shared/utils/format';
import { usePlaidConnections } from '../../linked-accounts/hooks/usePlaidConnections';
import { useWebull } from '../../webull/hooks/useWebull';
import { useTransactionSummary } from '../../transactions/hooks/useTransactionSummary';
import { summarizeNetWorth } from '../utils/netWorth';

const figureLabelClass = 'text-2xs font-semibold uppercase tracking-[0.08em] text-ink-muted';

/**
 * Everything you hold across linked banks and Webull, minus what the cards owe. Balance-sheet
 * data, so it sits apart from the budget figures and moves only when a sync lands.
 */
const NetWorthCard = () => {
  const { data: connections = [] } = usePlaidConnections();
  const { data: webull } = useWebull();
  // Pending sums per account, computed server-side; shares its cache with the uncategorized nudge.
  const { data: summary } = useTransactionSummary();

  const netWorth = useMemo(
    () => summarizeNetWorth(connections, webull?.balances ?? [], summary?.pendingByAccount),
    [connections, webull, summary],
  );

  if (!netWorth) return null;

  const lastSynced = connections
    .map((c) => c.lastSyncedAt)
    .filter((d): d is string => !!d)
    .sort()
    .at(-1);

  const figures = [
    { label: 'Cash', value: currencyPrecise.format(netWorth.cash) },
    { label: 'Investments', value: currencyPrecise.format(netWorth.investments) },
    { label: 'Cards owed', value: `−${currencyPrecise.format(netWorth.owed)}` },
  ];

  return (
    <Card
      title="Net worth"
      subtitle={
        lastSynced &&
        `Bank balances as of ${localDateTime(lastSynced)}` +
          (netWorth.pending !== 0 ? ` · includes ${signedCurrency(netWorth.pending)} pending` : '')
      }
      contentClassName="flex flex-wrap items-end justify-between gap-x-10 gap-y-4"
    >
      <p className="numeric text-3xl font-semibold tracking-[-0.02em] text-ink">
        {currencyPrecise.format(netWorth.total)}
      </p>
      <dl className="flex flex-wrap gap-8">
        {figures.map((figure) => (
          <div key={figure.label}>
            <dt className={figureLabelClass}>{figure.label}</dt>
            <dd className="numeric mt-1 text-lg font-semibold text-ink-muted">{figure.value}</dd>
          </div>
        ))}
      </dl>
    </Card>
  );
};

export default NetWorthCard;
