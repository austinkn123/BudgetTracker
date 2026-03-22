import { useQuery } from '@tanstack/react-query';
import { expenseService } from '../services/expense.service';
import type { Expense } from '../types/api';

export const useExpenses = () => {
  return useQuery<Expense[], Error>({
    queryKey: ['expenses'],
    queryFn: () => expenseService.getCurrentUserExpenses(),
    retry: 1,
  });
};
