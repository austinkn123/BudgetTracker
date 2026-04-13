import { useQuery } from '@tanstack/react-query';
import { userService } from '../../../shared/services/user.service';
import type { User } from '../../../shared/types/api';

export const useUser = () => {
  return useQuery<User, Error>({
    queryKey: ['user'],
    queryFn: () => userService.getCurrentUser(),
    retry: 1,
  });
};
