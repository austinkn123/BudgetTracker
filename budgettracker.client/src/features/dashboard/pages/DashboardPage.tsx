import { useState } from 'react';
import { useUser } from '../../user/hooks/useUser';
import { useCategories } from '../../categories/hooks/useCategories';
import { useTransactions } from '../../transactions/hooks/useTransactions';
import { useBudgetPlans } from '../../budget-plans/hooks/useBudgetPlans';
import { DashboardHeader } from '../components/DashboardHeader';
import { DashboardLoadingState } from '../components/DashboardLoadingState';
import { DashboardErrorState } from '../components/DashboardErrorState';
import { StatusBanner } from '../../../shared/components/StatusBanner';
import UserSection from '../../user/UserSection';
import CategoriesSection from '../../categories/CategoriesSection';
import TransactionsSection from '../../transactions/TransactionsSection';
import BudgetPlansSection from '../../budget-plans/BudgetPlansSection';

export default function DashboardPage() {
  const { isLoading: loadingUser, error: userError } = useUser();
  const { isLoading: loadingCategories, error: categoriesError } = useCategories();
  const { isLoading: loadingTransactions, error: transactionsError } = useTransactions();
  const { isLoading: loadingBudgetPlans, error: budgetPlansError } = useBudgetPlans();

  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);

  const isLoading = loadingCategories || loadingTransactions || loadingUser || loadingBudgetPlans;
  const hasErrors = categoriesError || transactionsError || userError || budgetPlansError;

  return (
    <div className="min-h-screen bg-gray-50">
      <DashboardHeader />

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {isLoading && <DashboardLoadingState />}

        {!isLoading && hasErrors && (
          <DashboardErrorState
            userError={userError}
            categoriesError={categoriesError}
            transactionsError={transactionsError}
            budgetPlansError={budgetPlansError}
          />
        )}

        {!isLoading && !hasErrors && (
          <div className="space-y-8">
            <StatusBanner statusMessage={statusMessage} statusError={statusError} />
            <UserSection isLoading={isLoading} />
            <CategoriesSection isLoading={isLoading} />
            <TransactionsSection
              isLoading={isLoading}
              setStatusMessage={setStatusMessage}
              setStatusError={setStatusError}
            />
            <BudgetPlansSection
              isLoading={isLoading}
              setStatusMessage={setStatusMessage}
              setStatusError={setStatusError}
            />
          </div>
        )}
      </main>
    </div>
  );
}
