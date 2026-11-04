import { useQuery, type QueryClient } from '@tanstack/react-query';
import { budgetPlanService } from '../../../shared/services/budgetPlan.service';
import type { BudgetPlan } from '../../../shared/types/api';

export const useBudgetPlans = () => {
  return useQuery<BudgetPlan[], Error>({
    queryKey: ['budgetPlans'],
    queryFn: () => budgetPlanService.getCurrentUserBudgetPlans(),
    retry: 1,
  });
};

/** A plan change moves every figure measured against it, not just the plans list. */
export const invalidatePlanQueries = (queryClient: QueryClient) =>
  Promise.all(
    ['budgetPlans', 'money-map', 'budget-analysis'].map((key) =>
      queryClient.invalidateQueries({ queryKey: [key] }),
    ),
  );
