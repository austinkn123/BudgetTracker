import api from '../api';
import type { User } from '../types/api';

export const userService = {
  getCurrentUser: async (): Promise<User> => {
    const response = await api.get<User>('/users/me');
    return response.data;
  },
};
