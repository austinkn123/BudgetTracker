import api from '../lib/api';
import type { User } from '../types/api';

export const userService = {
  getById: async (id: number): Promise<User> => {
    const response = await api.get<User>(`/users/${id}`);
    return response.data;
  },
  
  getByCognitoId: async (cognitoId: string): Promise<User> => {
    const response = await api.get<User>(`/users/cognito/${cognitoId}`);
    return response.data;
  },
  
  create: async (user: Omit<User, 'id'>): Promise<User> => {
    const response = await api.post<User>('/users', user);
    return response.data;
  },
  
  update: async (id: number, user: User): Promise<User> => {
    const response = await api.put<User>(`/users/${id}`, user);
    return response.data;
  },
  
  delete: async (id: number): Promise<void> => {
    await api.delete(`/users/${id}`);
  },
};
