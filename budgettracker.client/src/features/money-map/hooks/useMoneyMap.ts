import { useQuery } from '@tanstack/react-query';
import { moneyMapService } from '../../../shared/services/moneyMap.service';
import type { MoneyMap } from '../../../shared/types/api';

export const useMoneyMap = (months: number) =>
  useQuery<MoneyMap, Error>({
    queryKey: ['money-map', months],
    queryFn: () => moneyMapService.get(months),
    retry: 1,
    refetchOnWindowFocus: true,
  });
