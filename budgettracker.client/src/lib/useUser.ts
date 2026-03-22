import { useQuery } from '@tanstack/react-query';
import { userService } from '../services/user.service';
import type { User } from '../types/api';

export const useUser = () => {
  return useQuery<User, Error>({
    queryKey: ['user'],
    queryFn: () => userService.getCurrentUser(),
    retry: 1,
  });
};
