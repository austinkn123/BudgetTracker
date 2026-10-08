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
import { useLinkedAccounts } from './useLinkedAccounts';

// Mock the axios instance (not plaidService) so the real service builds the URLs under test.
vi.mock('../../../shared/api', () => ({
  default: { get: vi.fn(), post: vi.fn(), delete: vi.fn() },
}));

const apiMock = vi.mocked(api, { deep: true });

const renderLinkedAccounts = (queryClient: QueryClient) =>
  renderHook(() => useLinkedAccounts(), { wrapper: createQueryWrapper(queryClient) });

describe('useLinkedAccounts', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.resetAllMocks();
    queryClient = createTestQueryClient();
    apiMock.get.mockResolvedValue({ data: [CHASE, ALLY] });
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

  describe('exchangePublicToken', () => {
    it("surfaces the server's { error } message when the institution is already linked", async () => {
      apiMock.post.mockRejectedValue(axiosBadRequest(ALREADY_LINKED_MESSAGE));

      const { result } = renderLinkedAccounts(queryClient);

      await act(() => result.current.exchangePublicToken('public-sandbox-token'));

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

      await act(() => result.current.exchangePublicToken('public-sandbox-token'));

      expect(result.current.errorMessage).toBe(
        "Couldn't finish linking your bank. Please try again.",
      );
    });

    it('clearError dismisses the message', async () => {
      apiMock.post.mockRejectedValue(axiosBadRequest(ALREADY_LINKED_MESSAGE));

      const { result } = renderLinkedAccounts(queryClient);
      await act(() => result.current.exchangePublicToken('public-sandbox-token'));
      expect(result.current.errorMessage).toBe(ALREADY_LINKED_MESSAGE);

      act(() => result.current.clearError());

      expect(result.current.errorMessage).toBeNull();
    });
  });
});
