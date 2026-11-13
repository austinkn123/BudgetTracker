import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { cashFlowService } from '../../../shared/services/cashFlow.service';
import type { CashFlowQuery, CashFlowReport } from '../../../shared/types/api';

export const useCashFlow = (query: CashFlowQuery) =>
  useQuery<CashFlowReport, Error>({
    queryKey: ['cash-flow', query],
    queryFn: () => cashFlowService.getReport(query),
    retry: 1,
    // Changing a control keeps the last chart up instead of dropping back to a skeleton.
    placeholderData: keepPreviousData,
  });
