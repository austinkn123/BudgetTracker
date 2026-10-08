import type { PlaidConnectionView } from '../shared/types/api';

export const ALREADY_LINKED_MESSAGE =
  'This institution is already linked. Disconnect it first if you want to link it again.';

export const CHASE: PlaidConnectionView = {
  plaidItemId: 7,
  institutionName: 'Chase',
  lastSyncedAt: '2026-09-27T12:00:00Z',
  accounts: [{ plaidAccountId: 'acc-chase-1', name: 'Checking', mask: '1234', accountType: 'depository' }],
};

export const ALLY: PlaidConnectionView = {
  plaidItemId: 42,
  institutionName: 'Ally Bank',
  lastSyncedAt: null,
  accounts: [{ plaidAccountId: 'acc-ally-1', name: 'Savings', mask: null, accountType: 'depository' }],
};

/** Shape of the axios error the server's 400 `{ error }` response produces. */
export const axiosBadRequest = (error: string) =>
  Object.assign(new Error('Request failed with status code 400'), {
    isAxiosError: true,
    response: { status: 400, data: { error } },
  });

/** A promise whose settlement the test controls, for asserting in-flight state. */
export const deferred = <T>() => {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
};
