import { useMemo } from 'react';
import { useCategories } from '../../categories/hooks/useCategories';
import { useTransactions } from '../../transactions/hooks/useTransactions';
import { useUser } from '../../user/hooks/useUser';
import DashboardLoadingState from '../components/DashboardLoadingState';
import DashboardErrorState from '../components/DashboardErrorState';
import RangeSelector from '../components/RangeSelector';
import PlanStoryHero from '../components/PlanStoryHero';
import PlanStoryHeroEmpty from '../components/PlanStoryHeroEmpty';
import CashflowWaterfall from '../components/CashflowWaterfall';
import WhereItWent from '../components/WhereItWent';
import CategoryDrillGrid from '../components/CategoryDrillGrid';
import BucketBreakdown from '../components/BucketBreakdown';
import RecentActivityFeed from '../components/RecentActivityFeed';
import { useDateRange } from '../hooks/useDateRange';
import { useBudgetAnalysis } from '../hooks/useBudgetAnalysis';
import { getStatusHeadline } from '../utils/planCopy';
import {
  selectCategoryCards,
  selectDrifting,
  selectRecentActivity,
  selectTopSpend,
  selectWaterfall,
} from '../utils/selectors';

// How much of the server's analysis this particular screen chooses to show.
const TREND_MONTHS = 3;
const WATERFALL_CATEGORIES = 5;
const SPEND_CATEGORIES = 8;
const DRIFTING_CATEGORIES = 3;
const RECENT_ACTIVITY_ITEMS = 8;
const DRILL_TRANSACTIONS = 10;

const DashboardPage = () => {
  const { isLoading: loadingUser, error: userError } = useUser();
  const { data: categories = [], isLoading: loadingCategories, error: categoriesError } = useCategories();
  const { data: transactions = [], isLoading: loadingTransactions, error: transactionsError } = useTransactions();
  const { range, setRange, start, end } = useDateRange();
  const { data: analysis, isLoading: loadingAnalysis, error: analysisError } =
    useBudgetAnalysis(start, end, TREND_MONTHS);

  const categoryNames = useMemo(
    () => new Map(categories.map((c) => [c.id, c.name])),
    [categories],
  );

  const windowTransactions = useMemo(() => {
    const startMs = start.getTime();
    const endMs = end.getTime();
    return transactions.filter((t) => {
      const ts = new Date(t.occurredAt).getTime();
      return ts >= startMs && ts <= endMs;
    });
  }, [transactions, start, end]);

  const planMonth = analysis?.planMonth ?? null;

  const hero = useMemo(() => {
    if (!planMonth) return null;
    return {
      plan: planMonth.plan,
      pacing: planMonth.pacing,
      headline: getStatusHeadline(
        planMonth.pacing.pacingDelta,
        planMonth.pacing.daysPct,
        planMonth.plan.planMonth,
      ),
      drifting: selectDrifting(planMonth.byCategory, categoryNames, DRIFTING_CATEGORIES),
    };
  }, [planMonth, categoryNames]);

  const waterfall = useMemo(
    () => (planMonth ? selectWaterfall(planMonth, categoryNames, WATERFALL_CATEGORIES) : []),
    [planMonth, categoryNames],
  );

  const categoryCards = useMemo(
    () =>
      planMonth
        ? selectCategoryCards(
            planMonth,
            analysis?.monthlyTrend ?? [],
            windowTransactions,
            categoryNames,
            DRILL_TRANSACTIONS,
          )
        : [],
    [planMonth, analysis?.monthlyTrend, windowTransactions, categoryNames],
  );

  const topSpend = useMemo(
    () => selectTopSpend(analysis?.windowSpend ?? [], categoryNames, SPEND_CATEGORIES),
    [analysis?.windowSpend, categoryNames],
  );

  const recentActivity = useMemo(
    () => selectRecentActivity(windowTransactions, RECENT_ACTIVITY_ITEMS),
    [windowTransactions],
  );

  const isLoading = loadingUser || loadingCategories || loadingTransactions || loadingAnalysis;
  const hasErrors = userError || categoriesError || transactionsError || analysisError;

  if (isLoading) return <DashboardLoadingState />;

  if (hasErrors || !analysis) {
    return (
      <DashboardErrorState
        userError={userError}
        categoriesError={categoriesError}
        transactionsError={transactionsError}
        analysisError={analysisError}
      />
    );
  }

  return (
    <div className="space-y-8">
      {/* Header row: title + range selector */}
      <div className="flex flex-col gap-4 rounded-md border border-border bg-surface px-5 py-5 shadow-sm sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-ink">Dashboard</h1>
          {hero && (
            <p className="mt-1 text-sm text-ink-muted">
              Tracking against <span className="font-semibold text-ink">{hero.plan.name}</span>
            </p>
          )}
        </div>
        <RangeSelector value={range} onChange={setRange} />
      </div>

      {/* Hero */}
      {hero ? (
        <PlanStoryHero
          plan={hero.plan}
          pacing={hero.pacing}
          headline={hero.headline}
          drifting={hero.drifting}
        />
      ) : (
        <PlanStoryHeroEmpty />
      )}

      {/* Cashflow waterfall + Where it went */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <CashflowWaterfall bars={waterfall} />
        </div>
        <WhereItWent rows={topSpend} />
      </div>

      {/* Category drill grid */}
      {planMonth && <CategoryDrillGrid cards={categoryCards} />}

      {/* Bucket breakdown + recent activity */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <BucketBreakdown rows={planMonth?.byBucket ?? []} />
        <RecentActivityFeed items={recentActivity} categories={categories} />
      </div>
    </div>
  );
};

export default DashboardPage;
