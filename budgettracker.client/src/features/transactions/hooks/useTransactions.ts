import { useQuery } from '@tanstack/react-query';
import { transactionService } from '../../../shared/services/transaction.service';
import type { Transaction } from '../../../shared/types/api';

export const useTransactions = () => {
  return useQuery<Transaction[], Error>({
    queryKey: ['transactions'],
    queryFn: () => transactionService.getCurrentUserTransactions(),
    retry: 1,
  });
};
