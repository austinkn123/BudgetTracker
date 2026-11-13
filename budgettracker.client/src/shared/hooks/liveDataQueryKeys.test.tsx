import { renderHook, waitFor } from '@testing-library/react';
import type { QueryKey } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';
import { createQueryWrapper, createTestQueryClient } from '../../test/renderWithQueryClient';
import { useBudgetPlans } from '../../features/budget-plans/hooks/useBudgetPlans';
import { useCashFlow } from '../../features/cash-flow/hooks/useCashFlow';
import { useCategories } from '../../features/categories/hooks/useCategories';
import { useCategoryUsage } from '../../features/categories/hooks/useCategoryUsage';
import { useBudgetAnalysis } from '../../features/dashboard/hooks/useBudgetAnalysis';
import { usePlaidConnections } from '../../features/linked-accounts/hooks/usePlaidConnections';
import { useMoneyMap } from '../../features/money-map/hooks/useMoneyMap';
import { useTransactionFeed } from '../../features/transactions/hooks/useTransactionFeed';
import { useTransactionPage, useTransactions } from '../../features/transactions/hooks/useTransactions';
import { useTransactionSummary } from '../../features/transactions/hooks/useTransactionSummary';
import { useUser } from '../../features/user/hooks/useUser';
import { useWebull, useWebullHoldings, useWebullTrades } from '../../features/webull/hooks/useWebull';
import { isLiveDataQuery, useDataVersionSync } from './useDataVersionSync';

/*
 * Contract between isLiveDataQuery and the keys the app's hooks really build. Each hook is rendered
 * and the key is read back from the cache, so renaming a hook's key without updating LIVE_PREFIXES
 * fails here — no key literal is repeated in this file.
 */

// Every request resolves to an empty page: enough for the feed to succeed and seed its head key.
vi.mock('../api', () => ({
  default: { get: vi.fn(async () => ({ data: { items: [], nextCursor: null, version: 1 } })) },
}));

/** Render a hook on its own client and return every key it put in the cache. */
const keysOf = async (useHook: () => unknown): Promise<QueryKey[]> => {
  const queryClient = createTestQueryClient();
  renderHook(useHook, { wrapper: createQueryWrapper(queryClient) });
  await waitFor(() =>
    expect(queryClient.getQueryCache().getAll().every((q) => q.state.fetchStatus === 'idle')).toBe(true),
  );
  return queryClient.getQueryCache().getAll().map((q) => q.queryKey);
};

const SINGLE_KEY_LIVE_HOOKS: [string, () => unknown][] = [
  ['useTransactions', () => useTransactions({ from: '2026-11-01', to: '2026-11-30' })],
  ['useTransactionPage', () => useTransactionPage({}, 5)],
  ['useTransactionSummary', useTransactionSummary],
  ['useCategoryUsage', () => useCategoryUsage('2026-01-01')],
  ['useBudgetAnalysis', () => useBudgetAnalysis(new Date(2026, 10, 1), new Date(2026, 10, 30), 6)],
  ['useCashFlow', () => useCashFlow({ to: '2026-11-30', groupBy: 'Month' })],
  ['useMoneyMap', () => useMoneyMap(3)],
  ['useCategories', useCategories],
  ['useBudgetPlans', useBudgetPlans],
  ['usePlaidConnections', usePlaidConnections],
];

const SINGLE_KEY_UNTOUCHED_HOOKS: [string, () => unknown][] = [
  ['useUser', useUser],
  ['useWebull', useWebull],
  ['useWebullHoldings', useWebullHoldings],
  ['useWebullTrades', () => useWebullTrades('1M')],
  ['useDataVersionSync', useDataVersionSync],
];

describe('isLiveDataQuery against the keys the hooks build', () => {
  it.each(SINGLE_KEY_LIVE_HOOKS)('treats the %s key as live', async (_name, useHook) => {
    const keys = await keysOf(useHook);
    expect(keys).toHaveLength(1);
    expect(isLiveDataQuery(keys[0])).toBe(true);
  });

  it.each(SINGLE_KEY_UNTOUCHED_HOOKS)('leaves the %s key alone', async (_name, useHook) => {
    const keys = await keysOf(useHook);
    expect(keys).toHaveLength(1);
    expect(isLiveDataQuery(keys[0])).toBe(false);
  });

  it('treats the feed head as live and leaves the infinite feed itself alone', async () => {
    const keys = await keysOf(() => useTransactionFeed({}));
    const infinite = keys.filter((k) => k[k.length - 1] !== 'head');
    const head = keys.filter((k) => k[k.length - 1] === 'head');

    expect(infinite).toHaveLength(1);
    expect(head).toHaveLength(1);
    expect(isLiveDataQuery(infinite[0])).toBe(false);
    expect(isLiveDataQuery(head[0])).toBe(true);
  });

  it('covers every hook file that reads through a query', () => {
    // A new query hook must be classified above (live or not) before this passes.
    const sources = import.meta.glob<string>('../../features/**/hooks/*.ts', {
      query: '?raw',
      import: 'default',
      eager: true,
    });
    const withKeys = Object.entries(sources)
      .filter(([path, src]) => !path.endsWith('.test.ts') && /\buse(Infinite|Suspense)?Quer(y|ies)\s*[<(]/.test(src))
      .map(([path]) => path.split('/').pop())
      .sort();

    expect(withKeys).toEqual([
      'useBudgetAnalysis.ts',
      'useBudgetPlans.ts',
      'useCashFlow.ts',
      'useCategories.ts',
      'useCategoryUsage.ts',
      'useMoneyMap.ts',
      'usePlaidConnections.ts',
      'useTransactionFeed.ts',
      'useTransactionSummary.ts',
      'useTransactions.ts',
      'useUser.ts',
      'useWebull.ts',
    ]);
  });
});
