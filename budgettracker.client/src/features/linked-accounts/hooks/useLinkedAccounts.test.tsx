import type { QueryClient } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import api from '../../../shared/api';
import {
  ALLY,
  ALREADY_LINKED_MESSAGE,
  CHASE,
  axiosBadRequest,
  deferred,
} from '../../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { loadPlaidLink, useLinkedAccounts } from './useLinkedAccounts';
import { PLAID_CONNECTIONS_QUERY_KEY } from './usePlaidConnections';

// Mock the axios instance (not plaidService) so the real service builds the URLs under test.
vi.mock('../../../shared/api', () => ({
  default: { get: vi.fn(), post: vi.fn(), delete: vi.fn() },
}));

const apiMock = vi.mocked(api, { deep: true });

const renderLinkedAccounts = (queryClient: QueryClient) =>
  renderHook(() => useLinkedAccounts(), { wrapper: createQueryWrapper(queryClient) });

const postedUrls = () => apiMock.post.mock.calls.map(([url]) => url);

const SYNC_SUMMARY = { inserted: 3, updated: 0, removed: 0, syncedAt: '2026-10-10T12:00:00Z' };

describe('useLinkedAccounts', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.resetAllMocks();
    localStorage.clear();
    queryClient = createTestQueryClient();
    apiMock.get.mockResolvedValue({ data: [CHASE, ALLY] });
  });

  describe('reconnect', () => {
    const reconnectUrl = `/plaid/connections/${CHASE.plaidItemId}/link-token`;
    const reconnectedUrl = `/plaid/connections/${CHASE.plaidItemId}/reconnected`;

    it('opens Link in update mode, then marks it reconnected and syncs instead of exchanging', async () => {
      apiMock.post.mockImplementation(async (url: string) => {
        if (url === reconnectUrl) return { data: { linkToken: 'link-update-token', expiration: '' } };
        if (url === reconnectedUrl) return { data: undefined };
        if (url === '/plaid/sync') return { data: SYNC_SUMMARY };
        throw new Error(`Unexpected POST ${url}`);
      });
      const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderLinkedAccounts(queryClient);

      await act(() => result.current.prepareReconnect(CHASE.plaidItemId));

      expect(result.current.linkToken).toBe('link-update-token');
      expect(result.current.reconnectingItemId).toBe(CHASE.plaidItemId);
      // Saved so an OAuth redirect can resume the same update-mode session.
      expect(loadPlaidLink()).toEqual({
        linkToken: 'link-update-token',
        reconnectItemId: CHASE.plaidItemId,
      });

      await act(() => result.current.handleLinkSuccess('public-update-token'));

      // Update mode keeps the existing access token, so nothing is exchanged. The flag is cleared
      // before the sync, so a sync that still fails flags the bank again.
      expect(postedUrls()).toEqual([reconnectUrl, reconnectedUrl, '/plaid/sync']);
      expect(result.current.linkToken).toBeNull();
      expect(result.current.reconnectingItemId).toBeNull();
      expect(result.current.lastSync).toEqual(SYNC_SUMMARY);
      expect(loadPlaidLink()).toBeNull();
      expect(invalidate).toHaveBeenCalledWith({ queryKey: PLAID_CONNECTIONS_QUERY_KEY });
    });

    it('reports the error and skips the sync when the connection cannot be marked reconnected', async () => {
      apiMock.post.mockImplementation(async (url: string) => {
        if (url === reconnectUrl) return { data: { linkToken: 'link-update-token', expiration: '' } };
        throw new Error('Request failed with status code 404');
      });
      const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderLinkedAccounts(queryClient);
      await act(() => result.current.prepareReconnect(CHASE.plaidItemId));
      await act(() => result.current.handleLinkSuccess('public-update-token'));

      expect(postedUrls()).toEqual([reconnectUrl, reconnectedUrl]);
      expect(result.current.reconnectingItemId).toBeNull();
      expect(result.current.errorMessage).toBe(
        "Couldn't finish reconnecting your bank. Please try again.",
      );
      expect(invalidate).toHaveBeenCalledWith({ queryKey: PLAID_CONNECTIONS_QUERY_KEY });
    });

    it('forgets the reconnect on exit, so the next Link session adds an institution', async () => {
      apiMock.post.mockImplementation(async (url: string) => {
        if (url === reconnectUrl) return { data: { linkToken: 'link-update-token', expiration: '' } };
        if (url === '/plaid/link-token') return { data: { linkToken: 'link-sandbox-token', expiration: '' } };
        if (url === '/plaid/exchange-token') return { data: SYNC_SUMMARY };
        throw new Error(`Unexpected POST ${url}`);
      });

      const { result } = renderLinkedAccounts(queryClient);
      await act(() => result.current.prepareReconnect(CHASE.plaidItemId));

      act(() => result.current.clearLinkToken());

      expect(result.current.reconnectingItemId).toBeNull();
      expect(loadPlaidLink()).toBeNull();

      await act(() => result.current.prepareLink());
      await act(() => result.current.handleLinkSuccess('pt'));

      expect(postedUrls()).toEqual([reconnectUrl, '/plaid/link-token', '/plaid/exchange-token']);
      expect(apiMock.post).toHaveBeenLastCalledWith('/plaid/exchange-token', { publicToken: 'pt' });
    });

    it('clears the reconnecting id and reports an error when the token request fails', async () => {
      apiMock.post.mockRejectedValue(new Error('Request failed with status code 404'));

      const { result } = renderLinkedAccounts(queryClient);

      await act(() => result.current.prepareReconnect(CHASE.plaidItemId));

      expect(result.current.reconnectingItemId).toBeNull();
      expect(result.current.linkToken).toBeNull();
      expect(result.current.errorMessage).toBe(
        "Couldn't start reconnecting your bank. Please try again.",
      );
      expect(loadPlaidLink()).toBeNull();
    });
  });

  describe('prepareLink', () => {
    it('saves the new-institution token for an OAuth redirect and forgets it on exit', async () => {
      apiMock.post.mockResolvedValue({ data: { linkToken: 'link-sandbox-token', expiration: '' } });

      const { result } = renderLinkedAccounts(queryClient);

      await act(() => result.current.prepareLink());

      expect(postedUrls()).toEqual(['/plaid/link-token']);
      expect(loadPlaidLink()).toEqual({ linkToken: 'link-sandbox-token', reconnectItemId: null });

      act(() => result.current.clearLinkToken());

      expect(result.current.linkToken).toBeNull();
      expect(loadPlaidLink()).toBeNull();
    });

    it("shows a ProblemDetails response's detail, which is how the link-token routes report errors", async () => {
      const detail = 'A bank connection needs to be reconnected. Disconnect it and link it again.';
      apiMock.post.mockRejectedValue(
        Object.assign(new Error('Request failed with status code 502'), {
          isAxiosError: true,
          response: { status: 502, data: { title: 'Bad Gateway', status: 502, detail } },
        }),
      );

      const { result } = renderLinkedAccounts(queryClient);
      await act(() => result.current.prepareLink());

      expect(result.current.errorMessage).toBe(detail);
    });
  });

  describe('disconnect', () => {
    it('calls DELETE /plaid/connections/{id} and marks only that id as disconnecting', async () => {
      const pendingDelete = deferred<{ data: undefined }>();
      apiMock.delete.mockReturnValue(pendingDelete.promise);

      const { result } = renderLinkedAccounts(queryClient);
      await waitFor(() => expect(result.current.connections).toHaveLength(2));
      expect(result.current.disconnectingItemId).toBeNull();

      let disconnectDone!: Promise<void>;
      act(() => {
        disconnectDone = result.current.disconnect(ALLY.plaidItemId);
      });

      await waitFor(() => expect(result.current.disconnectingItemId).toBe(ALLY.plaidItemId));
      expect(result.current.disconnectingItemId).not.toBe(CHASE.plaidItemId);
      expect(apiMock.delete).toHaveBeenCalledTimes(1);
      expect(apiMock.delete).toHaveBeenCalledWith(`/plaid/connections/${ALLY.plaidItemId}`);

      await act(async () => {
        pendingDelete.resolve({ data: undefined });
        await disconnectDone;
      });

      await waitFor(() => expect(result.current.disconnectingItemId).toBeNull());
      expect(result.current.errorMessage).toBeNull();
    });

    it('clears disconnectingItemId and reports an error when the delete fails', async () => {
      apiMock.delete.mockRejectedValue(new Error('Network Error'));

      const { result } = renderLinkedAccounts(queryClient);
      await waitFor(() => expect(result.current.connections).toHaveLength(2));

      await act(() => result.current.disconnect(CHASE.plaidItemId));

      expect(result.current.disconnectingItemId).toBeNull();
      expect(result.current.errorMessage).toBe("Couldn't disconnect your bank. Please try again.");
    });
  });

  describe('handleLinkSuccess for a new institution', () => {
    it("surfaces the server's { error } message when the institution is already linked", async () => {
      apiMock.post.mockRejectedValue(axiosBadRequest(ALREADY_LINKED_MESSAGE));

      const { result } = renderLinkedAccounts(queryClient);

      await act(() => result.current.handleLinkSuccess('public-sandbox-token'));

      expect(apiMock.post).toHaveBeenCalledWith('/plaid/exchange-token', {
        publicToken: 'public-sandbox-token',
      });
      expect(result.current.errorMessage).toBe(ALREADY_LINKED_MESSAGE);
      expect(result.current.isExchanging).toBe(false);
      expect(result.current.lastSync).toBeNull();
    });

    it('falls back to a generic message when the server sends no { error }', async () => {
      apiMock.post.mockRejectedValue(new Error('Network Error'));

      const { result } = renderLinkedAccounts(queryClient);

      await act(() => result.current.handleLinkSuccess('public-sandbox-token'));

      expect(result.current.errorMessage).toBe(
        "Couldn't finish linking your bank. Please try again.",
      );
    });

    it('clearError dismisses the message', async () => {
      apiMock.post.mockRejectedValue(axiosBadRequest(ALREADY_LINKED_MESSAGE));

      const { result } = renderLinkedAccounts(queryClient);
      await act(() => result.current.handleLinkSuccess('public-sandbox-token'));
      expect(result.current.errorMessage).toBe(ALREADY_LINKED_MESSAGE);

      act(() => result.current.clearError());

      expect(result.current.errorMessage).toBeNull();
    });
  });
});
