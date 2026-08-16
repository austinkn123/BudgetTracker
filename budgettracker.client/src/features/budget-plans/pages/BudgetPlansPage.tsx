import { useState } from 'react';
import { useBudgetPlans } from '../hooks/useBudgetPlans';
import { useCategories } from '../../categories/hooks/useCategories';
import StatusBanner from '../../../shared/components/StatusBanner';
import BudgetPlansSection from '../BudgetPlansSection';
import CategoriesSection from '../../categories/CategoriesSection';

const BudgetPlansPage = () => {
  const { isLoading: loadingPlans } = useBudgetPlans();
  const { isLoading: loadingCategories } = useCategories();
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);

  const isLoading = loadingPlans || loadingCategories;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-ink">Budget Plans</h1>
        <p className="mt-1 text-sm text-ink-muted">
          Create and manage your monthly budget plans
        </p>
      </div>

      <StatusBanner statusMessage={statusMessage} statusError={statusError} />

      <CategoriesSection
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
  );
};

export default BudgetPlansPage;
