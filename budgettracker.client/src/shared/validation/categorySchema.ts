import { z } from 'zod';

export const categorySchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Category name is required')
    .max(100, 'Category name must be 100 characters or fewer'),
  categoryType: z.enum(['Income', 'Expense', 'Both']),
  /**
   * Optional Plaid taxonomy mapping. '' means "no mapping" and is sent as an explicit empty
   * string so the server can tell "clear it" apart from "field not supplied" (which preserves).
   */
  plaidCategoryPrimary: z.string().optional(),
});

export type CategoryFormValues = z.infer<typeof categorySchema>;
