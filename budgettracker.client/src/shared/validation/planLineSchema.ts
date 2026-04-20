import { z } from 'zod';

export const planLineSchema = z.object({
  categoryId: z.number().int().positive('Category is required'),
  bucket: z.enum(['Core', 'Buffer']),
  cadence: z.enum(['Monthly', 'Annual']),
  amount: z.number().positive('Amount must be greater than 0'),
  isStressFactor: z.boolean(),
  notes: z.string().max(500, 'Notes must be 500 characters or fewer'),
});

export type PlanLineFormData = z.infer<typeof planLineSchema>;
