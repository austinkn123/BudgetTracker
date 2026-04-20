import { z } from 'zod';

export const transactionSchema = z.object({
  accountId: z.number().int().positive(),
  categoryId: z.number().int().positive('Category is required'),
  amount: z.number().positive('Amount must be greater than 0'),
  occurredAt: z.string().min(1, 'Date is required'),
  payee: z.string().max(200, 'Payee must be 200 characters or fewer'),
  notes: z.string().max(500, 'Notes must be 500 characters or fewer'),
});

export type TransactionFormData = z.infer<typeof transactionSchema>;
