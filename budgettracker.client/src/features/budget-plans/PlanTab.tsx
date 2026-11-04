import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ListChecks } from 'lucide-react';
import { Button, Card } from '../../shared/components/ui';
import { useBudgetPlans } from './hooks/useBudgetPlans';
import { useCategories } from '../categories/hooks/useCategories';
import StatusBanner from '../../shared/components/StatusBanner';
import BudgetPlansSection from './BudgetPlansSection';
import CategoriesSection from '../categories/CategoriesSection';

/** Money Map's Plan tab: what you intend to spend each month, and the categories it is measured in. */
const PlanTab = () => {
  const { data: plans = [], isLoading: loadingPlans } = useBudgetPlans();
  const { isLoading: loadingCategories } = useCategories();
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);

  const isLoading = loadingPlans || loadingCategories;

  return (
    <div className="stagger-in space-y-6">
      <StatusBanner statusMessage={statusMessage} statusError={statusError} />

      <Card>
        <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-semibold text-ink">
              {plans.length > 0 ? 'Change your budget step by step' : 'Make your budget step by step'}
            </p>
            <p className="mt-1 text-sm text-ink-muted">
              Four short questions, with amounts filled in from your bank.
            </p>
          </div>
          <Button component={Link} to="/budget" startIcon={<ListChecks size={16} />}>
            {plans.length > 0 ? 'Change my budget' : 'Make my budget'}
          </Button>
        </div>
      </Card>

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

export default PlanTab;
