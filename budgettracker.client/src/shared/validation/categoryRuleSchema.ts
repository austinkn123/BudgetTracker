import { z } from 'zod';

/** "Always categorize this payee as" (BUD-24). Mirrors CategoryRuleEngine.ValidateRule on the server. */
export const categoryRuleSchema = z.object({
  pattern: z
    .string()
    .trim()
    .min(1, 'Pattern is required')
    .max(255, 'Pattern must be 255 characters or fewer'),
  /** Contains: anywhere in the payee. Exact: the whole payee. Both ignore case. */
  matchType: z.enum(['Contains', 'Exact']),
  categoryId: z.number({ error: 'Pick a category' }).int('Pick a category').positive('Pick a category'),
  /** Create only: also file the existing uncategorized rows this rule wins. */
  applyToExisting: z.boolean().default(false),
});

export type CategoryRuleFormInput = z.input<typeof categoryRuleSchema>;
export type CategoryRuleFormValues = z.output<typeof categoryRuleSchema>;
