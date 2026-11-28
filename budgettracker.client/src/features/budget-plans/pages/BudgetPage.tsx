import { useMemo } from 'react';
import { ErrorState, PageHeader, Skeleton } from '../../../shared/components/ui';
import { useMoneyMap } from '../../money-map/hooks/useMoneyMap';
import { AVERAGE_MONTHS, normalIncome, splitMonth } from '../../money-map/utils/moneyMap';
import BudgetTable from '../../money-map/components/BudgetTable';
import PlanTab from '../PlanTab';

/** The plan: every line against what it really costs, then the plan and categories to edit. */
const BudgetPage = () => {
  // Same query as Home's "normal month", so the figures match and the request is shared.
  const { data: map, isPending, error, refetch, isFetching } = useMoneyMap(AVERAGE_MONTHS);
  const split = useMemo(() => (map ? splitMonth(map) : null), [map]);

  return (
    <div className="stagger-in space-y-6">
      <PageHeader title="Budget" description="Your plan for each month, next to what you usually spend." />

      {error && !map ? (
        <ErrorState title="Couldn't load your budget" onRetry={() => void refetch()} isRetrying={isFetching} />
      ) : isPending || !map || !split ? (
        <Skeleton height={520} className="rounded-xl" />
      ) : (
        <BudgetTable lines={map.lines} months={map.months} income={normalIncome(map)} plan={split.plan} actual={split.actual} />
      )}

      <PlanTab />
    </div>
  );
};

export default BudgetPage;
