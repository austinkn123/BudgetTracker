import { describe, expect, it } from 'vitest';
import type { PlaidConnectionView, Transaction, WebullBalance } from '../../../shared/types/api';
import { summarizeNetWorth } from './netWorth';

const bank = (accounts: PlaidConnectionView['accounts']): PlaidConnectionView => ({
  plaidItemId: 1,
  institutionName: 'Bank of America',
  needsReconnect: false,
  accounts,
});

const webull = (value: number, currency = 'USD'): WebullBalance => ({
  accountId: 'w1',
  accountLabel: null,
  accountType: 'MARGIN',
  totalNetLiquidationValue: value,
  totalCashBalance: null,
  currency,
});

describe('summarizeNetWorth', () => {
  it('adds cash and investments and subtracts what the cards owe', () => {
    const result = summarizeNetWorth(
      [
        bank([
          { plaidAccountId: 'chk', name: 'Checking', accountType: 'depository', currentBalance: 8028.59 },
          { plaidAccountId: 'c1', name: 'Card', accountType: 'credit', currentBalance: 2098.9 },
          { plaidAccountId: 'c2', name: 'Card 2', accountType: 'credit', currentBalance: 639.74 },
          { plaidAccountId: 'new', name: 'Not synced', accountType: 'depository', currentBalance: null },
        ]),
      ],
      [webull(46102.79), webull(500, 'HKD')],
    );

    expect(result).toEqual({
      cash: 8028.59,
      investments: 46102.79,
      owed: expect.closeTo(2738.64, 2),
      total: expect.closeTo(51392.74, 2),
      pending: 0,
    });
  });

  it('counts pending activity, so a paycheck shows before it posts', () => {
    const pending = (plaidAccountId: string, amount: number): Transaction => ({
      id: amount,
      accountId: 1,
      transactionType: 'Income',
      categoryId: null,
      amount,
      occurredAt: '2026-10-30T00:00:00',
      createdAt: '2026-10-30T00:00:00',
      plaidAccountId,
      isPending: true,
    });

    const result = summarizeNetWorth(
      [
        bank([
          { plaidAccountId: 'chk', name: 'Checking', accountType: 'depository', currentBalance: 6061.16 },
          { plaidAccountId: 'card', name: 'Card', accountType: 'credit', currentBalance: 98.58 },
        ]),
      ],
      [],
      [
        pending('chk', 3262.8),
        pending('chk', -7.03),
        pending('card', -93.4),
        { ...pending('chk', -500), isPending: false }, // already in the posted balance
      ],
    );

    expect(result?.cash).toBeCloseTo(9316.93, 2);
    expect(result?.owed).toBeCloseTo(191.98, 2);
    expect(result?.pending).toBeCloseTo(3162.37, 2);
  });

  it('is null until some account reports a balance', () => {
    expect(
      summarizeNetWorth([bank([{ plaidAccountId: 'chk', name: 'Checking', accountType: 'depository' }])], []),
    ).toBeNull();
  });
});
