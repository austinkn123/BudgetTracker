import { StrictMode, type ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { focusManager, type QueryClient, type QueryKey } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { dataVersionService } from '../services/dataVersion.service';
import { createQueryWrapper, createTestQueryClient } from '../../test/renderWithQueryClient';
import { DATA_VERSION_QUERY_KEY, isLiveDataQuery, useDataVersionSync } from './useDataVersionSync';

vi.mock('../services/dataVersion.service', () => ({
  dataVersionService: { getVersion: vi.fn() },
}));

const getVersion = vi.mocked(dataVersionService.getVersion);

const FEED_KEY = ['transactions', 'feed', {}] as const;

/** Every live budget-data key the app reads, plus the head of the feed. */
const LIVE_KEYS: QueryKey[] = [
  ['transactions', { month: '2026-11' }],
  ['transactions', 'page', {}, 5],
  ['transactions', 'summary'],
  ['transactions', 'category-usage', '2026-01-01'],
  [...FEED_KEY, 'head'],
  ['budget-analysis', '2026-11-01', '2026-11-30', 6],
  ['cash-flow', { months: 6 }],
  ['money-map', 3],
  ['categories'],
  ['budgetPlans'],
  ['plaid', 'connections'],
];

/** Keys a data-version bump must leave alone. */
const UNTOUCHED_KEYS: QueryKey[] = [
  FEED_KEY,
  DATA_VERSION_QUERY_KEY,
  ['user'],
  ['webull'],
  ['webull-holdings'],
  ['webull-trades', '1M'],
];

const seed = (queryClient: QueryClient) => {
  // data-version is seeded by the hook itself.
  [...LIVE_KEYS, ...UNTOUCHED_KEYS.filter((k) => k !== DATA_VERSION_QUERY_KEY)].forEach((key) =>
    queryClient.setQueryData(key, { seeded: true }),
  );
};

/**
 * Keys marked invalidated. data-version itself is excluded: TanStack flags any query whose fetch
 * errored as invalidated, and the hook never invalidates it (see the isLiveDataQuery cases).
 */
const invalidated = (queryClient: QueryClient) =>
  queryClient
    .getQueryCache()
    .getAll()
    .filter((q) => q.state.isInvalidated && q.queryKey[0] !== DATA_VERSION_QUERY_KEY[0])
    .map((q) => q.queryKey);

/** Wait for a check to land, not merely start, so a following check does not cancel it. */
const settledAt = (queryClient: QueryClient, version: number) =>
  waitFor(() => expect(queryClient.getQueryData(DATA_VERSION_QUERY_KEY)).toEqual({ version }));

const setup = () => {
  const queryClient = createTestQueryClient();
  seed(queryClient);
  const hook = renderHook(() => useDataVersionSync(), { wrapper: createQueryWrapper(queryClient) });
  return { queryClient, ...hook };
};

/** Run one more version check, as the interval or a refocus would. */
const check = (queryClient: QueryClient) =>
  act(() => queryClient.refetchQueries({ queryKey: DATA_VERSION_QUERY_KEY }));

beforeEach(() => {
  getVersion.mockReset();
});

afterEach(() => {
  focusManager.setFocused(undefined);
  vi.useRealTimers();
});

describe('isLiveDataQuery', () => {
  it.each(LIVE_KEYS.map((k) => [JSON.stringify(k), k]))('treats %s as live', (_label, key) => {
    expect(isLiveDataQuery(key)).toBe(true);
  });

  it.each(UNTOUCHED_KEYS.map((k) => [JSON.stringify(k), k]))('leaves %s alone', (_label, key) => {
    expect(isLiveDataQuery(key)).toBe(false);
  });
});

describe('useDataVersionSync', () => {
  it('takes the first version as the baseline without invalidating anything', async () => {
    getVersion.mockResolvedValue({ version: 7 });
    const { queryClient } = setup();

    await settledAt(queryClient, 7);
    await act(async () => {});

    expect(getVersion).toHaveBeenCalledTimes(1);
    expect(invalidated(queryClient)).toEqual([]);
  });

  it('invalidates nothing when the version is unchanged', async () => {
    getVersion.mockResolvedValue({ version: 7 });
    const { queryClient } = setup();
    await settledAt(queryClient, 7);

    await check(queryClient);

    expect(getVersion).toHaveBeenCalledTimes(2);
    expect(invalidated(queryClient)).toEqual([]);
  });

  it.each([
    ['up', 8],
    ['down (after a restore)', 3],
  ])('invalidates every live key, and only those, when the version moves %s', async (_label, next) => {
    getVersion.mockResolvedValueOnce({ version: 7 }).mockResolvedValue({ version: next });
    const { queryClient } = setup();
    await settledAt(queryClient, 7);

    await check(queryClient);

    await waitFor(() => expect(invalidated(queryClient)).toHaveLength(LIVE_KEYS.length));
    expect(invalidated(queryClient)).toEqual(expect.arrayContaining(LIVE_KEYS));
    UNTOUCHED_KEYS.forEach((key) => expect(invalidated(queryClient)).not.toContainEqual(key));
  });

  it('invalidates once per change, then takes the new version as the baseline', async () => {
    getVersion.mockResolvedValueOnce({ version: 7 }).mockResolvedValue({ version: 8 });
    const { queryClient } = setup();
    await settledAt(queryClient, 7);
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    await check(queryClient);
    await waitFor(() => expect(invalidate).toHaveBeenCalledTimes(1));
    await check(queryClient);

    expect(getVersion).toHaveBeenCalledTimes(3);
    expect(invalidate).toHaveBeenCalledTimes(1);
  });

  it('does nothing on an error, keeps the baseline, and acts on the next changed version', async () => {
    getVersion
      .mockResolvedValueOnce({ version: 7 })
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValue({ version: 8 });
    const { queryClient } = setup();
    await settledAt(queryClient, 7);

    await check(queryClient);
    await waitFor(() => expect(queryClient.getQueryState(DATA_VERSION_QUERY_KEY)?.status).toBe('error'));
    // No retry: the error settles on the one request.
    expect(getVersion).toHaveBeenCalledTimes(2);
    expect(invalidated(queryClient)).toEqual([]);

    await check(queryClient);
    await waitFor(() => expect(invalidated(queryClient)).toHaveLength(LIVE_KEYS.length));
  });

  it('takes a cached version as the baseline on remount', async () => {
    getVersion.mockResolvedValue({ version: 7 });
    const { queryClient, unmount } = setup();
    await settledAt(queryClient, 7);
    unmount();

    renderHook(() => useDataVersionSync(), { wrapper: createQueryWrapper(queryClient) });
    await waitFor(() => expect(getVersion).toHaveBeenCalledTimes(2));
    await act(async () => {});

    expect(invalidated(queryClient)).toEqual([]);
  });

  it('invalidates live keys on remount when the version moved while unmounted', async () => {
    getVersion.mockResolvedValueOnce({ version: 7 }).mockResolvedValue({ version: 8 });
    const { queryClient, unmount } = setup();
    await settledAt(queryClient, 7);
    unmount();

    // The cached 7 is the baseline; the remount's own check sees 8, a change made while away.
    renderHook(() => useDataVersionSync(), { wrapper: createQueryWrapper(queryClient) });
    await settledAt(queryClient, 8);

    await waitFor(() => expect(invalidated(queryClient)).toHaveLength(LIVE_KEYS.length));
    expect(invalidated(queryClient)).toEqual(expect.arrayContaining(LIVE_KEYS));
  });

  it('takes the first successful version as the baseline when the first check errors', async () => {
    getVersion.mockRejectedValueOnce(new Error('offline')).mockResolvedValue({ version: 8 });
    const { queryClient } = setup();
    await waitFor(() => expect(queryClient.getQueryState(DATA_VERSION_QUERY_KEY)?.status).toBe('error'));

    await check(queryClient);
    await settledAt(queryClient, 8);
    await act(async () => {});

    expect(getVersion).toHaveBeenCalledTimes(2);
    expect(invalidated(queryClient)).toEqual([]);
  });

  it('under StrictMode takes one baseline, invalidates nothing and polls on one interval', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    getVersion.mockResolvedValue({ version: 7 });
    const queryClient = createTestQueryClient();
    seed(queryClient);
    const QueryWrapper = createQueryWrapper(queryClient);
    const wrapper = ({ children }: { children: ReactNode }) => (
      <StrictMode>
        <QueryWrapper>{children}</QueryWrapper>
      </StrictMode>
    );
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
    renderHook(() => useDataVersionSync(), { wrapper });

    await settledAt(queryClient, 7);
    await act(async () => {});
    expect(getVersion).toHaveBeenCalledTimes(1);

    // A duplicated interval would show up as two checks per tick.
    await act(() => vi.advanceTimersByTimeAsync(20_000));
    expect(getVersion).toHaveBeenCalledTimes(2);
    await act(() => vi.advanceTimersByTimeAsync(20_000));
    expect(getVersion).toHaveBeenCalledTimes(3);

    expect(invalidate).not.toHaveBeenCalled();
    expect(invalidated(queryClient)).toEqual([]);
  });

  it('checks every 20 seconds while focused, and not at all while the tab is hidden', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    getVersion.mockResolvedValue({ version: 7 });
    const { queryClient } = setup();
    await settledAt(queryClient, 7);

    await act(() => vi.advanceTimersByTimeAsync(20_000));
    expect(getVersion).toHaveBeenCalledTimes(2);

    act(() => focusManager.setFocused(false));
    await act(() => vi.advanceTimersByTimeAsync(120_000));
    expect(getVersion).toHaveBeenCalledTimes(2);
  });

  it('checks when the window regains focus', async () => {
    getVersion.mockResolvedValueOnce({ version: 7 }).mockResolvedValue({ version: 8 });
    const { queryClient } = setup();
    await settledAt(queryClient, 7);

    act(() => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });

    await waitFor(() => expect(getVersion).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(invalidated(queryClient)).toHaveLength(LIVE_KEYS.length));
  });
});
