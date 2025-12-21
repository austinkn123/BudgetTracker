import { useQuery } from '@tanstack/react-query';
import { userService } from '../services/user.service';
import type { User } from '../types/api';

export function useUser(userId: number) {
  return useQuery<User, Error>({
    queryKey: ['user', userId],
    queryFn: () => userService.getById(userId),
    retry: 1,
  });
}
