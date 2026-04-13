import { useQuery } from '@tanstack/react-query';
import { budgetPlanService } from '../services/budgetPlan.service';
import type { BudgetPlan } from '../types/api';

export const useBudgetPlans = () => {
  return useQuery<BudgetPlan[], Error>({
    queryKey: ['budgetPlans'],
    queryFn: () => budgetPlanService.getCurrentUserBudgetPlans(),
    retry: 1,
  });
};