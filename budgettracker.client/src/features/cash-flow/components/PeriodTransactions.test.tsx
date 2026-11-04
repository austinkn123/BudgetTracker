import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { Transaction } from '../../../shared/types/api';
import PeriodTransactions from './PeriodTransactions';

const txn = (id: number, transactionType: string, amount: number, payee: string): Transaction => ({
  id,
  accountId: 1,
  transactionType,
  categoryId: null,
  amount,
  occurredAt: '2026-10-03T00:00:00',
  createdAt: '2026-10-03T00:00:00',
  payee,
});

vi.mock('../../transactions/hooks/useTransactions', () => ({
  useTransactions: () => ({
    data: [
      txn(1, 'Income', 2500, 'Gusto'),
      txn(2, 'Expense', -60, 'Kroger'),
      txn(3, 'Expense', 15, 'Kroger refund'),
      txn(4, 'Transfer', -500, 'To savings'),
      txn(5, 'Adjustment', -12, 'Balance correction'),
    ],
    isLoading: false,
    error: null,
  }),
}));

describe('PeriodTransactions', () => {
  it('lists only the Income and Expense rows the bar is made of', () => {
    render(
      <PeriodTransactions
        period={{ start: '2026-10-03', moneyIn: 2500, moneyOut: 45, net: 2455, balance: null }}
        grouping="Day"
        bounds={{ from: new Date(2026, 9, 3), to: new Date(2026, 9, 3) }}
        plaidAccountId={null}
        categoryId={null}
        search=""
        categories={[]}
        onClear={() => undefined}
      />,
    );

    expect(screen.getByText('Gusto')).toBeInTheDocument();
    expect(screen.getByText('Kroger')).toBeInTheDocument();
    expect(screen.getByText('Kroger refund')).toBeInTheDocument();
    expect(screen.queryByText('To savings')).not.toBeInTheDocument();
    expect(screen.queryByText('Balance correction')).not.toBeInTheDocument();
  });
});
