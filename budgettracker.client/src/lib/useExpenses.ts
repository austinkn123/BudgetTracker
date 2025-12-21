import { useQuery } from '@tanstack/react-query';
import { expenseService } from '../services/expense.service';
import type { Expense } from '../types/api';

export const useExpenses = (userId: number) => {
  return useQuery<Expense[], Error>({
    queryKey: ['expenses', userId],
    queryFn: () => expenseService.getByUserId(userId),
    retry: 1,
  });
};
