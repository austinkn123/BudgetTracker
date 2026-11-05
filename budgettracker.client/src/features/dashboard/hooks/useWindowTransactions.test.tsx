import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { transactionService } from '../../../shared/services/transaction.service';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { ALL_TIME_LATEST_LIMIT, useWindowTransactions } from './useWindowTransactions';

vi.mock('../../../shared/services/transaction.service', () => ({
  transactionService: { getFiltered: vi.fn(), getPage: vi.fn() },
}));

const getFiltered = vi.mocked(transactionService.getFiltered);
const getPage = vi.mocked(transactionService.getPage);

const render = (...args: Parameters<typeof useWindowTransactions>) =>
  renderHook(() => useWindowTransactions(...args), {
    wrapper: createQueryWrapper(createTestQueryClient()),
  });

beforeEach(() => {
  getFiltered.mockReset().mockResolvedValue([]);
  getPage.mockReset().mockResolvedValue({ items: [], nextCursor: null });
});

describe('useWindowTransactions', () => {
  it('reads a bounded range server-side as inclusive calendar dates', async () => {
    const { result } = render('3m', new Date(2026, 8, 1), new Date(2026, 10, 30, 23, 59, 59, 999));

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getFiltered).toHaveBeenCalledWith({ from: '2026-09-01', to: '2026-11-30' });
    expect(getPage).not.toHaveBeenCalled();
  });

  it('reads the newest page for "all time" instead of a 1970–9999 window', async () => {
    const { result } = render('all', new Date(1970, 0, 1), new Date(9999, 11, 31));

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getPage).toHaveBeenCalledWith({}, { limit: ALL_TIME_LATEST_LIMIT });
    expect(getFiltered).not.toHaveBeenCalled();
  });
});
