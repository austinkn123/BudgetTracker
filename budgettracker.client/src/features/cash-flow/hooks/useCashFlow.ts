import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { cashFlowService } from '../../../shared/services/cashFlow.service';
import type { CashFlowQuery, CashFlowReport } from '../../../shared/types/api';

/** Matches useBudgetAnalysis' cadence so Plaid-synced rows surface without a manual refresh (BUD-6). */
const CASH_FLOW_REFETCH_INTERVAL_MS = 60_000;

export const useCashFlow = (query: CashFlowQuery) =>
  useQuery<CashFlowReport, Error>({
    queryKey: ['cash-flow', query],
    queryFn: () => cashFlowService.getReport(query),
    retry: 1,
    refetchInterval: CASH_FLOW_REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
    // Changing a control keeps the last chart up instead of dropping back to a skeleton.
    placeholderData: keepPreviousData,
  });
