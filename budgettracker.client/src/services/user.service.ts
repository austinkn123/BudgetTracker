import api from '../lib/api';
import type { User } from '../types/api';

export const userService = {
  getCurrentUser: async (): Promise<User> => {
    const response = await api.get<User>('/users/me');
    return response.data;
  },
  
  updateCurrentUser: async (user: User): Promise<User> => {
    const response = await api.put<User>('/users/me', user);
    return response.data;
  },
};
