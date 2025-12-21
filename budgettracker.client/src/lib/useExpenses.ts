import { useQuery } from '@tanstack/react-query';
import { expenseService } from '../services/api.service';
import type { Expense } from '../types/api';

export function useExpenses(userId: number) {
  return useQuery<Expense[], Error>({
    queryKey: ['expenses', userId],
    queryFn: () => expenseService.getByUserId(userId),
    retry: 1,
  });
}
