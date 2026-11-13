import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { transactionService } from '../../../shared/services/transaction.service';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { useCategoryDrillTransactions, useWindowTransactions, windowFilter } from './useWindowTransactions';

vi.mock('../../../shared/services/transaction.service', () => ({
  transactionService: { getFiltered: vi.fn(), getPage: vi.fn() },
}));

const getFiltered = vi.mocked(transactionService.getFiltered);
const getPage = vi.mocked(transactionService.getPage);

const wrapper = () => createQueryWrapper(createTestQueryClient());

const SEP_1 = new Date(2026, 8, 1);
const NOV_30 = new Date(2026, 10, 30, 23, 59, 59, 999);
const EPOCH = new Date(1970, 0, 1);
const FAR_FUTURE = new Date(9999, 11, 31);

beforeEach(() => {
  getFiltered.mockReset().mockResolvedValue([]);
  getPage.mockReset().mockResolvedValue({ items: [], nextCursor: null });
});

describe('windowFilter', () => {
  it('bounds a range by inclusive calendar dates', () => {
    expect(windowFilter('3m', SEP_1, NOV_30)).toEqual({ from: '2026-09-01', to: '2026-11-30' });
  });

  it('leaves "all time" unbounded rather than sending a 1970–9999 window', () => {
    expect(windowFilter('all', EPOCH, FAR_FUTURE)).toEqual({});
  });
});

describe('useWindowTransactions', () => {
  it('reads only the newest rows it shows for a bounded range', async () => {
    const { result } = renderHook(() => useWindowTransactions('3m', SEP_1, NOV_30, 8), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getPage).toHaveBeenCalledWith({ from: '2026-09-01', to: '2026-11-30' }, { limit: 8 });
    expect(getFiltered).not.toHaveBeenCalled();
  });

  it('reads the newest rows it shows for "all time"', async () => {
    const { result } = renderHook(() => useWindowTransactions('all', EPOCH, FAR_FUTURE, 8), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getPage).toHaveBeenCalledWith({}, { limit: 8 });
    expect(getFiltered).not.toHaveBeenCalled();
  });
});

describe('useCategoryDrillTransactions', () => {
  it('reads one category\'s newest rows across all history for "all time"', async () => {
    const { result } = renderHook(() => useCategoryDrillTransactions(4, {}, 10), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getPage).toHaveBeenCalledWith({ categoryId: 4 }, { limit: 10 });
  });

  it('narrows a bounded range to the category', async () => {
    const rangeFilter = { from: '2026-09-01', to: '2026-11-30' };
    const { result } = renderHook(() => useCategoryDrillTransactions(4, rangeFilter, 10), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getPage).toHaveBeenCalledWith({ ...rangeFilter, categoryId: 4 }, { limit: 10 });
  });

  it('reads the uncategorized card through the uncategorized filter', async () => {
    const { result } = renderHook(() => useCategoryDrillTransactions(null, {}, 10), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getPage).toHaveBeenCalledWith({ uncategorized: true }, { limit: 10 });
  });

  it('fetches nothing while disabled', () => {
    renderHook(() => useCategoryDrillTransactions(4, {}, 10, { enabled: false }), {
      wrapper: wrapper(),
    });

    expect(getPage).not.toHaveBeenCalled();
  });
});
