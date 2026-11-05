import type { PendingByAccount, PlaidConnectionView, WebullBalance } from '../../../shared/types/api';

export interface NetWorth {
  /** Checking and savings. */
  cash: number;
  /** Brokerage value, Webull plus any Plaid-linked investment account. */
  investments: number;
  /** Card and loan balances, as a positive amount owed. */
  owed: number;
  total: number;
  /** Signed net of pending activity folded into the figures above (money in positive). */
  pending: number;
}

const OWED_TYPES = new Set(['credit', 'loan']);

/**
 * What you have minus what you owe, from each source's latest balance. Null when no account has
 * reported a balance yet, so the card never shows a made-up $0.
 *
 * Plaid's current balance counts posted activity only, so a paycheck that is still pending would
 * be missing. Each account's pending net (summed server-side, see /transactions/summary) is added
 * on top, which is how the bank computes its available balance.
 */
export const summarizeNetWorth = (
  connections: PlaidConnectionView[],
  webullBalances: WebullBalance[],
  pendingRows: PendingByAccount[] = [],
): NetWorth | null => {
  const pendingByAccount = new Map<string, number>();
  for (const row of pendingRows) {
    pendingByAccount.set(row.plaidAccountId, (pendingByAccount.get(row.plaidAccountId) ?? 0) + row.amount);
  }

  let cash = 0;
  let investments = 0;
  let owed = 0;
  let pending = 0;
  let known = false;

  for (const account of connections.flatMap((c) => c.accounts)) {
    if (account.currentBalance == null) continue;
    known = true;
    // Signed like every Transaction amount: money in positive, so a card charge (negative) adds to what is owed.
    const pendingNet = pendingByAccount.get(account.plaidAccountId) ?? 0;
    pending += pendingNet;
    const type = account.accountType.toLowerCase();
    if (OWED_TYPES.has(type)) owed += account.currentBalance - pendingNet;
    else if (type === 'investment') investments += account.currentBalance + pendingNet;
    else cash += account.currentBalance + pendingNet;
  }

  // ponytail: USD only; a foreign-currency brokerage account is left out rather than converted.
  for (const balance of webullBalances) {
    if (balance.currency !== 'USD') continue;
    known = true;
    investments += balance.totalNetLiquidationValue;
  }

  return known ? { cash, investments, owed, total: cash + investments - owed, pending } : null;
};
