import api from '../api';
import type { MoneyMap } from '../types/api';
import { localToday } from '../utils/localToday';

export const moneyMapService = {
  /** `months` is the window the actuals average over, ending with the current month. */
  get: async (months: number): Promise<MoneyMap> => {
    const response = await api.get<MoneyMap>('/money-map', { params: { months, today: localToday() } });
    return response.data;
  },
};
