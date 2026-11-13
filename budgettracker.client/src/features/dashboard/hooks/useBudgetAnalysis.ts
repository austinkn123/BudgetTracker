import { useQuery } from '@tanstack/react-query';
import { format } from 'date-fns';
import { budgetAnalysisService } from '../../../shared/services/budgetAnalysis.service';
import type { BudgetAnalysis } from '../../../shared/types/api';

export const useBudgetAnalysis = (from: Date, to: Date, trendMonths: number) => {
  const fromKey = format(from, 'yyyy-MM-dd');
  const toKey = format(to, 'yyyy-MM-dd');

  return useQuery<BudgetAnalysis, Error>({
    queryKey: ['budget-analysis', fromKey, toKey, trendMonths],
    queryFn: () => budgetAnalysisService.getAnalysis(fromKey, toKey, trendMonths),
    retry: 1,
  });
};
