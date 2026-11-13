import { z } from 'zod';

export const categorySchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Category name is required')
    .max(100, 'Category name must be 100 characters or fewer'),
  /** Transfer: money moving between your own accounts or into investing; never counted as spending. */
  categoryType: z.enum(['Income', 'Expense', 'Both', 'Transfer']),
  /**
   * Optional Plaid taxonomy mapping. '' means "no mapping" and is sent as an explicit empty
   * string so the server can tell "clear it" apart from "field not supplied" (which preserves).
   * The Select's empty option writes null; callers map null/undefined to '' on submit.
   */
  plaidCategoryPrimary: z.string().nullish(),
});

export type CategoryFormValues = z.infer<typeof categorySchema>;
