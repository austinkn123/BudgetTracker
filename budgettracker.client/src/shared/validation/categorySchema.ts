import { z } from 'zod';

export const categorySchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Category name is required')
    .max(100, 'Category name must be 100 characters or fewer'),
  categoryType: z.enum(['Income', 'Expense', 'Both']),
});

export type CategoryFormValues = z.infer<typeof categorySchema>;
