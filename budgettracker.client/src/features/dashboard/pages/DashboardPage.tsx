import { useMemo } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { useCategories } from '../../categories/hooks/useCategories';
import { useTransactions } from '../../transactions/hooks/useTransactions';
import { useBudgetPlans } from '../../budget-plans/hooks/useBudgetPlans';
import { useUser } from '../../user/hooks/useUser';
import { DashboardLoadingState } from '../components/DashboardLoadingState';
import { DashboardErrorState } from '../components/DashboardErrorState';
import { RangeSelector } from '../components/RangeSelector';
import { PlanStoryHero } from '../components/PlanStoryHero';
import { PlanStoryHeroEmpty } from '../components/PlanStoryHeroEmpty';
import { CashflowWaterfall } from '../components/CashflowWaterfall';
import { WhereItWent } from '../components/WhereItWent';
import { CategoryDrillGrid } from '../components/CategoryDrillGrid';
import { BucketBreakdown } from '../components/BucketBreakdown';
import { RecentActivityFeed } from '../components/RecentActivityFeed';
import { useDateRange } from '../hooks/useDateRange';
import { usePlanProgress } from '../hooks/usePlanProgress';
import { filterTransactionsByRange } from '../utils/chartHelpers';

const DashboardPage = () => {
  const { isLoading: loadingUser, error: userError } = useUser();
  const { data: categories = [], isLoading: loadingCategories, error: categoriesError } = useCategories();
  const { data: transactions = [], isLoading: loadingTransactions, error: transactionsError } = useTransactions();
  const { data: budgetPlans = [], isLoading: loadingBudgetPlans, error: budgetPlansError } = useBudgetPlans();

  const { range, setRange, start, end } = useDateRange();

  const isLoading = loadingCategories || loadingTransactions || loadingUser || loadingBudgetPlans;
  const hasErrors = categoriesError || transactionsError || userError || budgetPlansError;

  const activePlan = useMemo(() => budgetPlans.find((p) => p.isActive), [budgetPlans]);
  const progress = usePlanProgress({ plan: activePlan, transactions, categories });

  const rangedTransactions = useMemo(
    () => filterTransactionsByRange(transactions, start, end),
    [transactions, start, end],
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
      {/* Header row: title + range selector */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: 2,
        }}
      >
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700 }}>
            Dashboard
          </Typography>
          {activePlan && (
            <Typography variant="body2" color="text.secondary">
              Active plan: {activePlan.name}
            </Typography>
          )}
        </Box>
        <RangeSelector value={range} onChange={setRange} />
      </Box>

      {/* Hero */}
      {activePlan && progress ? (
        <PlanStoryHero plan={activePlan} progress={progress} />
      ) : (
        <PlanStoryHeroEmpty />
      )}

      {/* Cashflow waterfall + Where it went */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <CashflowWaterfall
            plan={activePlan}
            transactions={transactions}
            categories={categories}
          />
        </div>
        <WhereItWent transactions={rangedTransactions} categories={categories} />
      </div>

      {/* Category drill grid */}
      {activePlan && (
        <CategoryDrillGrid
          plan={activePlan}
          transactions={transactions}
          categories={categories}
          start={start}
          end={end}
        />
      )}

      {/* Bucket breakdown + recent activity */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <BucketBreakdown plan={activePlan} transactions={transactions} />
        <RecentActivityFeed
          transactions={rangedTransactions}
          categories={categories}
        />
      </div>
    </div>
  );
};

export default DashboardPage;
