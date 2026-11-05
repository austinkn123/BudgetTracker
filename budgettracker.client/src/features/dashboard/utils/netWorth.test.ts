import { describe, expect, it } from 'vitest';
import type { PlaidConnectionView, WebullBalance } from '../../../shared/types/api';
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
    // Server-side per-account sums of pending rows (posted rows are already in the balance).
    const result = summarizeNetWorth(
      [
        bank([
          { plaidAccountId: 'chk', name: 'Checking', accountType: 'depository', currentBalance: 6061.16 },
          { plaidAccountId: 'card', name: 'Card', accountType: 'credit', currentBalance: 98.58 },
        ]),
      ],
      [],
      [
        { plaidAccountId: 'chk', amount: 3262.8 - 7.03 },
        { plaidAccountId: 'card', amount: -93.4 },
      ],
    );

    expect(result?.cash).toBeCloseTo(9316.93, 2);
    expect(result?.owed).toBeCloseTo(191.98, 2);
    expect(result?.pending).toBeCloseTo(3162.37, 2);
  });

  it('adds pending to investments and ignores accounts with no balance or no connection', () => {
    const result = summarizeNetWorth(
      [
        bank([
          { plaidAccountId: 'brk', name: 'Brokerage', accountType: 'investment', currentBalance: 1000 },
          { plaidAccountId: 'new', name: 'Not synced', accountType: 'depository', currentBalance: null },
        ]),
      ],
      [],
      [
        { plaidAccountId: 'brk', amount: 25 },
        { plaidAccountId: 'new', amount: 400 }, // no balance yet, so nothing to fold it into
        { plaidAccountId: 'gone', amount: -60 }, // account no longer linked
      ],
    );

    expect(result).toEqual({ cash: 0, investments: 1025, owed: 0, total: 1025, pending: 25 });
  });

  it('is null until some account reports a balance', () => {
    expect(
      summarizeNetWorth([bank([{ plaidAccountId: 'chk', name: 'Checking', accountType: 'depository' }])], []),
    ).toBeNull();
  });
});
