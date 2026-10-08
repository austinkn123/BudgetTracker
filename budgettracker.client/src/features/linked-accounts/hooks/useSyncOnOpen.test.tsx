import { QueryClient } from '@tanstack/react-query';
import { render, renderHook, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';

import { plaidService } from '../../../shared/services/plaid.service';
import type { PlaidSyncSummary } from '../../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { PLAID_CONNECTIONS_QUERY_KEY } from './usePlaidConnections';
import { useSyncOnOpen } from './useSyncOnOpen';

vi.mock('../../../shared/services/plaid.service', () => ({
  plaidService: { sync: vi.fn() },
}));

const syncMock = vi.mocked(plaidService.sync);

const summary = (overrides: Partial<PlaidSyncSummary> = {}): PlaidSyncSummary => ({
  inserted: 0,
  updated: 0,
  removed: 0,
  syncedAt: '2026-09-27T00:00:00Z',
  ...overrides,
});

const invalidatedKeys = (spy: MockInstance<QueryClient['invalidateQueries']>) =>
  spy.mock.calls.map(([filters]) => filters?.queryKey);

describe('useSyncOnOpen', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    syncMock.mockReset();
    queryClient = createTestQueryClient();
  });

  it('fires exactly one sync(6) per mount', async () => {
    syncMock.mockResolvedValue(summary());

    renderHook(() => useSyncOnOpen(), { wrapper: createQueryWrapper(queryClient) });

    await waitFor(() => expect(syncMock).toHaveBeenCalledTimes(1));
    expect(syncMock).toHaveBeenCalledWith(6);
  });

  it('fires exactly one sync(6) under StrictMode', async () => {
    syncMock.mockResolvedValue(summary());

    renderHook(() => useSyncOnOpen(), {
      wrapper: createQueryWrapper(queryClient),
      reactStrictMode: true,
    });

    await waitFor(() => expect(syncMock).toHaveBeenCalled());
    // Let any duplicate effect run before asserting the count.
    await waitFor(() => expect(queryClient.isMutating()).toBe(0));
    expect(syncMock).toHaveBeenCalledTimes(1);
    expect(syncMock).toHaveBeenCalledWith(6);
  });

  it('fires again on a fresh mount', async () => {
    syncMock.mockResolvedValue(summary());
    const wrapper = createQueryWrapper(queryClient);

    const first = renderHook(() => useSyncOnOpen(), { wrapper });
    await waitFor(() => expect(syncMock).toHaveBeenCalledTimes(1));
    first.unmount();

    renderHook(() => useSyncOnOpen(), { wrapper });
    await waitFor(() => expect(syncMock).toHaveBeenCalledTimes(2));
  });

  it('refreshes connections but not transactions or budget-analysis when nothing changed', async () => {
    syncMock.mockResolvedValue(summary());
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    renderHook(() => useSyncOnOpen(), { wrapper: createQueryWrapper(queryClient) });

    await waitFor(() => expect(invalidate).toHaveBeenCalled());
    const keys = invalidatedKeys(invalidate);
    expect(keys).toEqual([PLAID_CONNECTIONS_QUERY_KEY]);
    expect(keys).not.toContainEqual(['transactions']);
    expect(keys).not.toContainEqual(['budget-analysis']);
  });

  it.each([
    ['inserted', { inserted: 3 }],
    ['updated', { updated: 1 }],
    ['removed', { removed: 2 }],
  ])('invalidates transactions and budget-analysis when %s > 0', async (_label, overrides) => {
    syncMock.mockResolvedValue(summary(overrides));
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    renderHook(() => useSyncOnOpen(), { wrapper: createQueryWrapper(queryClient) });

    await waitFor(() => expect(invalidate).toHaveBeenCalledTimes(3));
    const keys = invalidatedKeys(invalidate);
    expect(keys).toContainEqual(['transactions']);
    expect(keys).toContainEqual(['budget-analysis']);
  });

  it('swallows a sync error without retrying or rendering an error', async () => {
    syncMock.mockRejectedValue(new Error('Plaid is down'));
    // Client default says "retry 3 times"; the hook must override it with retry: false.
    const retryingClient = new QueryClient({
      defaultOptions: { mutations: { retry: 3, retryDelay: 0 } },
    });
    const invalidate = vi.spyOn(retryingClient, 'invalidateQueries');

    const Host = () => {
      useSyncOnOpen();
      return <p>Dashboard content</p>;
    };
    const Wrapper = createQueryWrapper(retryingClient);

    render(
      <Wrapper>
        <Host />
      </Wrapper>,
    );

    await waitFor(() => {
      const [mutation] = retryingClient.getMutationCache().getAll();
      expect(mutation?.state.status).toBe('error');
    });

    expect(syncMock).toHaveBeenCalledTimes(1);
    expect(invalidate).not.toHaveBeenCalled();
    expect(screen.getByText('Dashboard content')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
