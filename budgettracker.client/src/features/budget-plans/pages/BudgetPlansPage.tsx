import { useState } from 'react';
import { PageHeader } from '../../../shared/components/ui';
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
    <div className="stagger-in space-y-6">
      <PageHeader
        title="Budget Plans"
        description="What you intend to spend each month, and the categories it is measured in."
      />

      <StatusBanner statusMessage={statusMessage} statusError={statusError} />

      <BudgetPlansSection
        isLoading={isLoading}
        setStatusMessage={setStatusMessage}
        setStatusError={setStatusError}
      />

      <CategoriesSection
        isLoading={isLoading}
        setStatusMessage={setStatusMessage}
        setStatusError={setStatusError}
      />
    </div>
  );
};

export default BudgetPlansPage;
