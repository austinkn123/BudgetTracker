import { useMemo } from 'react';
import { useCategories } from '../../categories/hooks/useCategories';
import { useTransactions } from '../../transactions/hooks/useTransactions';
import { useBudgetPlans } from '../../budget-plans/hooks/useBudgetPlans';
import { DashboardLoadingState } from '../components/DashboardLoadingState';
import { DashboardErrorState } from '../components/DashboardErrorState';
import { SummaryCards } from '../components/SummaryCards';
import { SpendingByCategoryChart } from '../components/charts/SpendingByCategoryChart';
import { SpendingOverTimeChart } from '../components/charts/SpendingOverTimeChart';
import { BudgetVsActualChart } from '../components/charts/BudgetVsActualChart';
import { RecentTransactions } from '../components/RecentTransactions';
import { ActiveBudgetPlanSummary } from '../components/ActiveBudgetPlanSummary';
import {
  computeSummaryTotals,
  aggregateByCategory,
  aggregateByMonth,
  budgetVsActual,
} from '../utils/chartHelpers';
import { useUser } from '../../user/hooks/useUser';

const DashboardPage = () => {
  const { isLoading: loadingUser, error: userError } = useUser();
  const { data: categories = [], isLoading: loadingCategories, error: categoriesError } = useCategories();
  const { data: transactions = [], isLoading: loadingTransactions, error: transactionsError } = useTransactions();
  const { data: budgetPlans = [], isLoading: loadingBudgetPlans, error: budgetPlansError } = useBudgetPlans();

  const isLoading = loadingCategories || loadingTransactions || loadingUser || loadingBudgetPlans;
  const hasErrors = categoriesError || transactionsError || userError || budgetPlansError;

  const summaryTotals = useMemo(() => computeSummaryTotals(transactions), [transactions]);
  const categoryData = useMemo(() => aggregateByCategory(transactions, categories), [transactions, categories]);
  const monthlyData = useMemo(() => aggregateByMonth(transactions), [transactions]);
  const activePlan = useMemo(() => budgetPlans.find((p) => p.isActive), [budgetPlans]);
  const budgetActualData = useMemo(
    () => budgetVsActual(activePlan, transactions, categories),
    [activePlan, transactions, categories],
  );

  if (isLoading) return <DashboardLoadingState />;

  if (hasErrors) {
    return (
      <DashboardErrorState
        userError={userError}
        categoriesError={categoriesError}
        transactionsError={transactionsError}
        budgetPlansError={budgetPlansError}
      />
    );
  }

  return (
    <div className="space-y-8">
      <SummaryCards totals={summaryTotals} />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <SpendingByCategoryChart data={categoryData} />
        <SpendingOverTimeChart data={monthlyData} />
      </div>

      <BudgetVsActualChart data={budgetActualData} planName={activePlan?.name} />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <RecentTransactions transactions={transactions} categories={categories} />
        <ActiveBudgetPlanSummary plan={activePlan} />
      </div>
    </div>
  );
};

export default DashboardPage;
