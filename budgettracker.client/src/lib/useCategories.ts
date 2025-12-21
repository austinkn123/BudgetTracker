import { useQuery } from '@tanstack/react-query';
import { categoryService } from '../services/category.service';
import type { Category } from '../types/api';

export function useCategories(userId: number) {
  return useQuery<Category[], Error>({
    queryKey: ['categories', userId],
    queryFn: () => categoryService.getByUserId(userId),
    retry: 1,
  });
}
