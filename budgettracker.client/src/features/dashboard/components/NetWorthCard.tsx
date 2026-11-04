import { useMemo } from 'react';
import { Card } from '../../../shared/components/ui';
import { currencyPrecise, localDateTime, signedCurrency } from '../../../shared/utils/format';
import { usePlaidConnections } from '../../linked-accounts/hooks/usePlaidConnections';
import { useWebull } from '../../webull/hooks/useWebull';
import { useTransactions } from '../../transactions/hooks/useTransactions';
import { summarizeNetWorth } from '../utils/netWorth';

const figureLabelClass = 'text-2xs font-semibold uppercase tracking-[0.08em] text-ink-muted';

/**
 * Everything you hold across linked banks and Webull, minus what the cards owe. Balance-sheet
 * data, so it sits apart from the budget figures and moves only when a sync lands.
 */
const NetWorthCard = () => {
  const { data: connections = [] } = usePlaidConnections();
  const { data: webull } = useWebull();
  // Same query key as the dashboard's ledger, so this shares its cache rather than fetching again.
  const { data: transactions = [] } = useTransactions();

  const netWorth = useMemo(
    () => summarizeNetWorth(connections, webull?.balances ?? [], transactions),
    [connections, webull, transactions],
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
