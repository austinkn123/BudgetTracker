import api from '../api';
import type { MoneyMap } from '../types/api';

export const moneyMapService = {
  /** `months` is the window the actuals average over, ending with the current month. */
  get: async (months: number): Promise<MoneyMap> => {
    const response = await api.get<MoneyMap>('/money-map', { params: { months } });
    return response.data;
  },
};
