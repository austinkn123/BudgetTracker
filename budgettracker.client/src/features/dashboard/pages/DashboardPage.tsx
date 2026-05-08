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
  computeSummaryTotalsFromPlan,
  aggregateByCategory,
  aggregateByCategoryFromPlan,
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
  const activePlan = useMemo(() => budgetPlans.find((p) => p.isActive), [budgetPlans]);

  const summaryTotals = useMemo(
    () => (activePlan ? computeSummaryTotalsFromPlan(activePlan) : computeSummaryTotals(transactions)),
    [activePlan, transactions],
  );
  const categoryData = useMemo(
    () => (activePlan
      ? aggregateByCategoryFromPlan(activePlan, categories)
      : aggregateByCategory(transactions, categories)),
    [activePlan, transactions, categories],
  );
  const monthlyData = useMemo(() => aggregateByMonth(transactions), [transactions]);
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
      <section className="rounded-2xl border border-slate-200/70 bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 px-6 py-7 text-white shadow-sm md:px-8">
        <p className="text-sm font-medium text-slate-200">Overview</p>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight md:text-3xl">Financial Dashboard</h1>
        <p className="mt-2 max-w-2xl text-sm text-slate-300 md:text-base">
          Track spending, compare budgets, and monitor monthly performance at a glance.
        </p>
      </section>

      <SummaryCards totals={summaryTotals} mode={activePlan ? 'planned' : 'actual'} />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <SpendingByCategoryChart data={categoryData} />
        <SpendingOverTimeChart data={monthlyData} />
      </div>

      <BudgetVsActualChart
        data={budgetActualData}
        planName={activePlan?.name}
        hasActivePlan={Boolean(activePlan)}
      />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <RecentTransactions transactions={transactions} categories={categories} />
        <ActiveBudgetPlanSummary plan={activePlan} />
      </div>
    </div>
  );
};

export default DashboardPage;
