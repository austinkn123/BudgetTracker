import { Navigate } from 'react-router-dom';
import { useBudgetPlans } from '../../budget-plans/hooks/useBudgetPlans';

/**
 * Right after a bank is linked: a person with no budget yet goes to the guide, which drafts one from the new
 * transactions. Anyone else goes to <paramref name="otherwise"/>, or stays put when it is not given.
 */
const FirstBudgetRedirect = ({ otherwise }: { otherwise?: string }) => {
  const plans = useBudgetPlans();
  if (plans.isPending) return null;
  if (plans.data?.length === 0) return <Navigate to="/budget/guide" replace />;
  return otherwise ? <Navigate to={otherwise} replace /> : null;
};

export default FirstBudgetRedirect;
