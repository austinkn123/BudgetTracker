import { useQuery } from '@tanstack/react-query';
import { format } from 'date-fns';
import { budgetAnalysisService } from '../../../shared/services/budgetAnalysis.service';
import type { BudgetAnalysis } from '../../../shared/types/api';

/** Matches useTransactions' cadence so Plaid-synced rows surface without a manual refresh (BUD-6). */
const ANALYSIS_REFETCH_INTERVAL_MS = 60_000;

export const useBudgetAnalysis = (from: Date, to: Date, trendMonths: number) => {
  const fromKey = format(from, 'yyyy-MM-dd');
  const toKey = format(to, 'yyyy-MM-dd');

  return useQuery<BudgetAnalysis, Error>({
    queryKey: ['budget-analysis', fromKey, toKey, trendMonths],
    queryFn: () => budgetAnalysisService.getAnalysis(fromKey, toKey, trendMonths),
    retry: 1,
    refetchInterval: ANALYSIS_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
  });
};
