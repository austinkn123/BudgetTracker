import { useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { endOfMonth, startOfDay, startOfMonth } from 'date-fns';
import { Link } from 'react-router-dom';
import { ListChecks } from 'lucide-react';
import { Button, Card, ErrorState, Skeleton } from '../../../shared/components/ui';
import type { CashFlowQuery, MoneyMap, PlanPerformance } from '../../../shared/types/api';
import { currency, percent } from '../../../shared/utils/format';
import { usePlaidConnections } from '../../linked-accounts/hooks/usePlaidConnections';
import { useSyncOnOpen } from '../../linked-accounts/hooks/useSyncOnOpen';
import ReconnectAlert from '../../linked-accounts/components/ReconnectAlert';
import ConnectBankPrompt from '../../linked-accounts/components/ConnectBankPrompt';
import { useCategories } from '../../categories/hooks/useCategories';
import { useTransactionSummary } from '../../transactions/hooks/useTransactionSummary';
import { useWebull, useWebullRefreshOnOpen } from '../../webull/hooks/useWebull';
import { summarizeNetWorth } from '../../dashboard/utils/netWorth';
import { getStatusHeadline } from '../../dashboard/utils/planCopy';
import { useBudgetAnalysis } from '../../dashboard/hooks/useBudgetAnalysis';
import NetWorthCard from '../../dashboard/components/NetWorthCard';
import StatStrip from '../../dashboard/components/StatStrip';
import type { Stat } from '../../dashboard/components/StatStrip';
import UncategorizedNudge from '../../dashboard/components/UncategorizedNudge';
import { useCashFlow } from '../../cash-flow/hooks/useCashFlow';
import { periodBounds, rangeWindow, savingsRate, toDateKey } from '../../cash-flow/utils/cashFlow';
import CashFlowChart from '../../cash-flow/components/CashFlowChart';
import BalanceChart from '../../cash-flow/components/BalanceChart';
import PeriodTransactions from '../../cash-flow/components/PeriodTransactions';
import { useMoneyMap } from '../hooks/useMoneyMap';
import { AVERAGE_MONTHS, biggestOverrun, billsSetAside, normalIncome, splitMonth } from '../utils/moneyMap';
import PlanVsActual from '../components/PlanVsActual';
import MoneyFlow from '../components/MoneyFlow';
import BillsAhead from '../components/BillsAhead';

/** The analysis endpoint's trend length; Home doesn't show the trend, so the smallest the Dashboard used. */
const TREND_MONTHS = 3;

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

const Section = ({ title, detail, children }: { title: string; detail?: ReactNode; children: ReactNode }) => (
  <section className="space-y-4">
    <div>
      <h2 className="text-xl font-semibold tracking-[-0.015em] text-ink">{title}</h2>
      {detail && <p className="mt-1 text-sm text-ink-muted">{detail}</p>}
    </div>
    {children}
  </section>
);

/** This month against the plan: the trail headline and four figures. Nothing without an active plan. */
const ThisMonth = ({ planMonth }: { planMonth: PlanPerformance }) => {
  const { pacing, analyzedMonth, income } = planMonth;
  const daysLeft = Math.max(0, pacing.daysInMonth - pacing.daysElapsed);
  const stats: Stat[] = [
    { label: 'Came in', value: income, format: currency.format, tone: 'positive', detail: 'so far this month' },
    { label: 'Spent', value: pacing.actualExpenses, format: currency.format, detail: `of ${currency.format(pacing.plannedExpenses)} planned` },
    pacing.remaining >= 0
      ? { label: 'Left to spend', value: pacing.remaining, format: currency.format, detail: 'before you go over plan' }
      : { label: 'Over plan', value: -pacing.remaining, format: currency.format, tone: 'negative', detail: 'more than you planned' },
    { label: 'Days left', value: daysLeft, format: (n) => String(Math.round(n)), detail: `of ${pacing.daysInMonth}` },
  ];

  return (
    <Section title={getStatusHeadline(pacing.pacingDelta, pacing.daysPct, analyzedMonth)} detail={`Measured against ${planMonth.plan.name}.`}>
      <StatStrip stats={stats} />
    </Section>
  );
};

/** Money in and out for each of the last 12 months, plus the bank balance under it. A month opens its transactions. */
const MonthByMonth = () => {
  const { data: categories = [] } = useCategories();
  const [selectedStart, setSelectedStart] = useState<string | null>(null);
  // Same window as Cash flow's "12 months" range, so a month's bucket bounds match.
  const bounds = useMemo(() => {
    const today = startOfDay(new Date());
    return rangeWindow('12m', today, { from: today, to: today });
  }, []);
  const query = useMemo<CashFlowQuery>(
    () => ({ from: bounds.from ? toDateKey(bounds.from) : undefined, to: toDateKey(bounds.to), groupBy: 'Month' }),
    [bounds],
  );
  const { data: report, isPending, error, refetch, isFetching } = useCashFlow(query);

  if (error && !report) {
    return (
      <Section title="The last 12 months">
        <ErrorState title="Couldn't load the last 12 months" onRetry={() => void refetch()} isRetrying={isFetching} />
      </Section>
    );
  }
  if (isPending || !report) return <Skeleton height={380} className="rounded-xl" />;

  const { net, moneyIn } = report.totals;
  const rate = savingsRate(report.totals);
  const kept =
    net >= 0
      ? `You kept ${currency.format(net)}${rate !== null ? `, ${percent(rate)} of what came in` : ''}.`
      : `You spent ${currency.format(-net)} more than came in (${currency.format(moneyIn)}).`;
  const selected = report.periods.find((p) => p.start === selectedStart) ?? null;

  return (
    <Section title="The last 12 months" detail={`${kept} Pick a month to see its transactions.`}>
      <CashFlowChart periods={report.periods} grouping="Month" selected={selected?.start ?? null} onSelect={setSelectedStart} />
      {selected && (
        <PeriodTransactions
          key={selected.start}
          period={selected}
          grouping="Month"
          bounds={periodBounds(selected.start, 'Month', bounds)}
          plaidAccountId={null}
          categoryId={null}
          search=""
          categories={categories}
          onClear={() => setSelectedStart(null)}
        />
      )}
      {report.currentBalance !== null && (
        <BalanceChart periods={report.periods} grouping="Month" currentBalance={report.currentBalance} />
      )}
    </Section>
  );
};

/**
 * Home: how this month is going, where a normal month's pay goes, the last 12 months, and what you have.
 * Replaces the old Money map, Dashboard and Cash flow pages. The plan itself lives on the Budget page.
 */
const MoneyMapPage = () => {
  // The home page, so it pulls fresh bank and Webull data on open; both are silent and refresh the queries below.
  useSyncOnOpen();
  useWebullRefreshOnOpen();

  const { data: map, isPending, error, refetch: refetchMap, isFetching: isFetchingMap } = useMoneyMap(AVERAGE_MONTHS);
  const now = new Date();
  const { data: analysis } = useBudgetAnalysis(startOfMonth(now), endOfMonth(now), TREND_MONTHS);
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
    // Flex gap, not space-y: the sr-only heading and an empty alerts block must not leave a gap at the top.
    <div className="stagger-in flex flex-col gap-10">
      {/* No visible header: the sections name themselves. Screen readers still get a page heading. */}
      <h1 className="sr-only">Home</h1>

      <div className="space-y-4 empty:hidden">
        <ConnectBankPrompt description="Link the bank you spend from and this page fills in with your spending, income and balances." />
        <ReconnectAlert />
        <UncategorizedNudge count={uncategorizedCount} />
      </div>

      {analysis?.planMonth && <ThisMonth planMonth={analysis.planMonth} />}

      <Section title="A normal month" detail="Your plan next to what a month really cost, on average, over the last 12 months.">
        {error && !map ? (
          <ErrorState title="Couldn't load your money map" onRetry={() => void refetchMap()} isRetrying={isFetchingMap} />
        ) : isPending || !map || !split ? (
          <div className="space-y-6" aria-busy>
            <Skeleton height={220} className="rounded-xl" />
            <Skeleton height={160} className="rounded-xl" />
          </div>
        ) : !map.lines.some((l) => l.planMonthly > 0) ? (
          // No plan (or one with no spending lines): every chart below would show 100% of pay as saving.
          <Card>
            <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="text-sm font-semibold text-ink">No budget plan yet</p>
                <p className="mt-1 text-sm text-ink-muted">Make one to see where a normal month’s pay goes.</p>
              </div>
              <Button component={Link} to="/budget/guide" startIcon={<ListChecks size={16} />}>
                Make my budget
              </Button>
            </div>
          </Card>
        ) : (
          <div className="space-y-6">
            <PlanVsActual income={normalIncome(map)} plan={split.plan} actual={split.actual} months={map.months} />
            <Insights map={map} cash={cash} />
            <MoneyFlow lines={map.lines} income={normalIncome(map)} />
            <BillsAhead upcoming={map.upcoming} setAside={billsSetAside(map.lines)} today={new Date(map.to)} />
          </div>
        )}
      </Section>

      <MonthByMonth />

      <Section title="What you have">
        <NetWorthCard />
      </Section>
    </div>
  );
};

export default MoneyMapPage;
