import api from '../api';
import type { DataVersion } from '../types/api';

export const dataVersionService = {
  getVersion: async (): Promise<DataVersion> => {
    const response = await api.get<DataVersion>('/data-version');
    return response.data;
  },
};
