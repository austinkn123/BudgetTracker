import { z } from 'zod';

export const budgetPlanSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Plan name is required')
    .max(100, 'Plan name must be 100 characters or fewer'),
  planMonth: z
    .string()
    .regex(/^\d{4}-\d{2}$/, 'Plan month is required'),
  netIncomeMonthly: z.number().min(0, 'Net income must be zero or greater'),
  isActive: z.boolean(),
});

export type BudgetPlanFormData = z.infer<typeof budgetPlanSchema>;
