import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ListChecks } from 'lucide-react';
import { Button, Card } from '../../shared/components/ui';
import { useBudgetPlans } from './hooks/useBudgetPlans';
import { useCategories } from '../categories/hooks/useCategories';
import StatusBanner from '../../shared/components/StatusBanner';
import LoadError from '../../shared/components/LoadError';
import BudgetPlansSection from './BudgetPlansSection';
import CategoriesSection from '../categories/CategoriesSection';

/** The Budget page's editing half: what you intend to spend each month, and the categories it is measured in. */
const PlanTab = () => {
  const plansQuery = useBudgetPlans();
  const categoriesQuery = useCategories();
  const plans = plansQuery.data ?? [];
  const loadingPlans = plansQuery.isLoading;
  const loadingCategories = categoriesQuery.isLoading;
  // Either read failing would make both sections below say "none yet", which is a guess, not the truth.
  const failed = (plansQuery.isError && !plansQuery.data) || (categoriesQuery.isError && !categoriesQuery.data);
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
          <Button component={Link} to="/budget/guide" startIcon={<ListChecks size={16} />}>
            {plans.length > 0 ? 'Change my budget' : 'Make my budget'}
          </Button>
        </div>
      </Card>

      {failed ? (
        <LoadError
          what="your plans and categories"
          onRetry={() => {
            void plansQuery.refetch();
            void categoriesQuery.refetch();
          }}
        />
      ) : (
        <>
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
        </>
      )}
    </div>
  );
};

export default PlanTab;
