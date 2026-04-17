import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { useBudgetPlans } from '../hooks/useBudgetPlans';
import { useCategories } from '../../categories/hooks/useCategories';
import { StatusBanner } from '../../../shared/components/StatusBanner';
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
        <Typography variant="h4" className="font-bold text-gray-900">
          Budget Plans
        </Typography>
        <Typography variant="body2" className="text-gray-500 mt-1">
          Create and manage your monthly budget plans
        </Typography>
      </div>

      <StatusBanner statusMessage={statusMessage} statusError={statusError} />

      <CategoriesSection isLoading={isLoading} />

      <BudgetPlansSection
        isLoading={isLoading}
        setStatusMessage={setStatusMessage}
        setStatusError={setStatusError}
      />
    </div>
  );
};

export default BudgetPlansPage;
