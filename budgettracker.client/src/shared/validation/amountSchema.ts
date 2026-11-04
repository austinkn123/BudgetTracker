import { z } from 'zod';

/** One money amount typed by hand. An empty number input arrives as NaN, which fails the type check. */
export const amountSchema = z.object({
  amount: z
    .number({ error: 'Type an amount, like 120' })
    .min(0, "The amount can't be below $0")
    .max(10_000_000, 'That amount is too big'),
});

export type AmountFormData = z.infer<typeof amountSchema>;
