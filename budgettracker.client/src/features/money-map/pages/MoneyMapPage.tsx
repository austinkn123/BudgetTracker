import { useMemo, useState } from 'react';
import { Alert, Card, PageHeader, Skeleton, Tabs, ToggleGroup } from '../../../shared/components/ui';
import { useTabParam } from '../../../shared/hooks/useTabParam';
import type { MoneyMap } from '../../../shared/types/api';
import { currency } from '../../../shared/utils/format';
import { usePlaidConnections } from '../../linked-accounts/hooks/usePlaidConnections';
import { useSyncOnOpen } from '../../linked-accounts/hooks/useSyncOnOpen';
import ReconnectAlert from '../../linked-accounts/components/ReconnectAlert';
import { useTransactionSummary } from '../../transactions/hooks/useTransactionSummary';
import { useWebull, useWebullRefreshOnOpen } from '../../webull/hooks/useWebull';
import { summarizeNetWorth } from '../../dashboard/utils/netWorth';
import NetWorthCard from '../../dashboard/components/NetWorthCard';
import UncategorizedNudge from '../../dashboard/components/UncategorizedNudge';
import { useMoneyMap } from '../hooks/useMoneyMap';
import { biggestOverrun, billsSetAside, normalIncome, splitMonth } from '../utils/moneyMap';
import PlanVsActual from '../components/PlanVsActual';
import MoneyFlow from '../components/MoneyFlow';
import BudgetTable from '../components/BudgetTable';
import BillsAhead from '../components/BillsAhead';
import PlanTab from '../../budget-plans/PlanTab';

// Every window ends with the current month, so "1" is this month to date.
const RANGE_OPTIONS = [
  { value: '1', label: 'This month' },
  { value: '3', label: '3 months' },
  { value: '6', label: '6 months' },
  { value: '12', label: '12 months' },
] as const;

const TABS = [
  { value: 'map', label: 'Map' },
  { value: 'plan', label: 'Plan' },
] as const;

/** How many months of must-pay bills a cash cushion should cover. */
const CUSHION_MONTHS = 3;

interface InsightProps {
  figure: string;
  headline: string;
  detail: string;
  accent: string;
}

const Insight = ({ figure, headline, detail, accent }: InsightProps) => (
  <Card contentClassName="flex h-full flex-col gap-1.5">
    <span className={`mb-1 h-1 w-7 rounded-full ${accent}`} aria-hidden />
    <p className="numeric text-3xl font-bold tracking-[-0.03em] text-ink">{figure}</p>
    <p className="text-sm font-semibold text-ink">{headline}</p>
    <p className="text-sm text-ink-muted">{detail}</p>
  </Card>
);

const Insights = ({ map, cash }: { map: MoneyMap; cash: number | null }) => {
  const income = normalIncome(map);
  const { plan } = splitMonth(map);
  const biggest = [...map.lines].filter((l) => l.bucket === 'Core').sort((a, b) => b.planMonthly - a.planMonthly)[0];
  const overrun = biggestOverrun(map.lines);
  const cushionMonths = cash !== null && plan.mustPay > 0 ? cash / plan.mustPay : null;

  return (
    <div className="grid gap-4 md:grid-cols-3">
      {biggest && (
        <Insight
          accent="bg-primary"
          figure={`${Math.round((biggest.planMonthly / income) * 100)}%`}
          headline={`of a normal month goes to ${biggest.name.toLowerCase()}`}
          detail={`${currency.format(biggest.planMonthly)} of ${currency.format(income)}. It is fixed for now, so the rest of the plan works around it.`}
        />
      )}
      {overrun ? (
        <Insight
          accent="bg-warning"
          figure={`${currency.format(overrun.actualMonthly)}/mo`}
          headline={`on ${overrun.name.toLowerCase()}, the line furthest over plan`}
          detail={`${overrun.topPayees.length ? `Mostly ${overrun.topPayees.join(', ')}. ` : ''}Holding it to ${currency.format(overrun.planMonthly)} frees about ${currency.format((overrun.actualMonthly - overrun.planMonthly) * 12)} a year.`}
        />
      ) : (
        <Insight accent="bg-success" figure="0" headline="lines over plan" detail="Every planned line is within 5% of its plan." />
      )}
      {cushionMonths !== null && (
        <Insight
          accent="bg-success"
          figure={`${cushionMonths.toFixed(1)} months`}
          headline="of must-pay bills covered by cash"
          detail={
            cushionMonths < CUSHION_MONTHS
              ? `${currency.format(cash!)} in the bank against ${currency.format(plan.mustPay)} a month. Build to ${CUSHION_MONTHS} months (${currency.format(plan.mustPay * CUSHION_MONTHS)}) before investing more.`
              : `${currency.format(cash!)} in the bank is past the ${CUSHION_MONTHS}-month mark, so extra savings can go to investing.`
          }
        />
      )}
    </div>
  );
};

/**
 * The whole picture on one page: a normal month's pay against the plan, where it flows, every line against
 * its average, and the lump-sum bills ahead. The Plan tab edits the plan and categories behind it.
 */
const MoneyMapPage = () => {
  // The home page, so it pulls fresh bank and Webull data on open; both are silent and refresh the queries below.
  useSyncOnOpen();
  useWebullRefreshOnOpen();

  // Map is the bare /; the plan behind it is /?tab=plan.
  const [tab, selectTab] = useTabParam(['map', 'plan']);
  const [months, setMonths] = useState<(typeof RANGE_OPTIONS)[number]['value']>('12');
  const { data: map, isPending, error } = useMoneyMap(Number(months));
  const { data: connections = [] } = usePlaidConnections();
  const { data: webull } = useWebull();
  const { data: summary } = useTransactionSummary();

  const cash = useMemo(
    () => summarizeNetWorth(connections, webull?.balances ?? [], summary?.pendingByAccount)?.cash ?? null,
    [connections, webull, summary],
  );
  const split = useMemo(() => (map ? splitMonth(map) : null), [map]);
  const uncategorizedCount = summary?.uncategorizedCount ?? 0;

  return (
    <div className="stagger-in space-y-6">
      <PageHeader
        title="Money map"
        description={
          tab === 'plan' ? (
            'The plan every figure on the map is measured against, and the categories it uses.'
          ) : map?.planName ? (
            <>
              Where a normal month’s pay goes, against <span className="font-semibold text-ink">{map.planName}</span>
            </>
          ) : (
            'Where a normal month’s pay goes.'
          )
        }
        actions={
          tab === 'map' && <ToggleGroup value={months} onChange={setMonths} options={RANGE_OPTIONS} ariaLabel="Average over" />
        }
      />

      <ReconnectAlert />

      <Tabs value={tab} onChange={selectTab} options={TABS} ariaLabel="Money map">
        {tab === 'plan' ? (
          <PlanTab />
        ) : (
          <div className="stagger-in space-y-6">
            <NetWorthCard />
            <UncategorizedNudge count={uncategorizedCount} />

            {error && !map ? (
              <Alert severity="error" title="Couldn't load your money map" message="Check that the server and database are running." />
            ) : isPending || !map || !split ? (
              <div className="space-y-6" aria-busy>
                <Skeleton height={220} className="rounded-xl" />
                <Skeleton height={160} className="rounded-xl" />
                <Skeleton height={520} className="rounded-xl" />
              </div>
            ) : (
              <>
                <PlanVsActual income={normalIncome(map)} plan={split.plan} actual={split.actual} months={map.months} />
                <Insights map={map} cash={cash} />
                <MoneyFlow lines={map.lines} income={normalIncome(map)} />
                <BudgetTable lines={map.lines} months={map.months} income={normalIncome(map)} plan={split.plan} actual={split.actual} />
                <BillsAhead upcoming={map.upcoming} setAside={billsSetAside(map.lines)} today={new Date(map.to)} />
              </>
            )}
          </div>
        )}
      </Tabs>
    </div>
  );
};

export default MoneyMapPage;
