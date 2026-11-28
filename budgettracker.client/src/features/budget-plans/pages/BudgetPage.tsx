import { useMemo } from 'react';
import { PageHeader, Skeleton } from '../../../shared/components/ui';
import LoadError from '../../../shared/components/LoadError';
import { useMoneyMap } from '../../money-map/hooks/useMoneyMap';
import { AVERAGE_MONTHS, normalIncome, splitMonth } from '../../money-map/utils/moneyMap';
import BudgetTable from '../../money-map/components/BudgetTable';
import PlanTab from '../PlanTab';

/** The plan: every line against what it really costs, then the plan and categories to edit. */
const BudgetPage = () => {
  // Same query as Home's "normal month", so the figures match and the request is shared.
  const { data: map, isPending, error, refetch } = useMoneyMap(AVERAGE_MONTHS);
  const split = useMemo(() => (map ? splitMonth(map) : null), [map]);

  return (
    <div className="stagger-in space-y-6">
      <PageHeader title="Budget" description="Your plan for each month, next to what you usually spend." />

      {error && !map ? (
        <LoadError what="your plan next to your spending" onRetry={() => void refetch()} />
      ) : isPending || !map || !split ? (
        <Skeleton height={520} className="rounded-xl" />
      ) : !map.lines.some((l) => l.planMonthly > 0) ? null : ( // No plan yet: a table of $0s. PlanTab offers to make one.
        <BudgetTable lines={map.lines} months={map.months} income={normalIncome(map)} plan={split.plan} actual={split.actual} />
      )}

      <PlanTab />
    </div>
  );
};

export default BudgetPage;
