import { act, renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { categoryService } from '../../../shared/services/category.service';
import type { Category } from '../../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { useCategoryMutations } from './useCategoryMutations';

vi.mock('../../../shared/services/category.service', () => ({
  categoryService: { create: vi.fn(), update: vi.fn(), delete: vi.fn() },
}));

const GROCERIES: Category = { id: 4, userId: 1, name: 'Groceries', categoryType: 'Expense' };

const setup = () => {
  const queryClient = createTestQueryClient();
  queryClient.setQueryData(['categories'], [GROCERIES]);
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
  const { result } = renderHook(
    () => useCategoryMutations({ setStatusMessage: vi.fn(), setStatusError: vi.fn() }),
    { wrapper: createQueryWrapper(queryClient) },
  );
  const invalidatedKeys = () => invalidate.mock.calls.map(([filters]) => filters?.queryKey);
  return { result, invalidatedKeys };
};

beforeEach(() => {
  vi.mocked(categoryService.create).mockReset().mockResolvedValue(GROCERIES);
  vi.mocked(categoryService.update).mockReset().mockResolvedValue(GROCERIES);
  vi.mocked(categoryService.delete).mockReset().mockResolvedValue();
});

describe('useCategoryMutations cache refresh', () => {
  it('refreshes ledger-derived reads (summary, category usage) after a delete', async () => {
    const { result, invalidatedKeys } = setup();

    await act(() => result.current.deleteCategory.mutateAsync(GROCERIES.id));

    expect(invalidatedKeys()).toEqual(
      expect.arrayContaining([['categories'], ['money-map'], ['transactions']]),
    );
  });

  it('refreshes the ledger and its analyses when a retype moves its rows', async () => {
    const { result, invalidatedKeys } = setup();

    // The server reclassifies the category's rows to follow an Income/Expense change.
    await act(() =>
      result.current.updateCategory.mutateAsync({ ...GROCERIES, categoryType: 'Income' }),
    );

    expect(invalidatedKeys()).toEqual(
      expect.arrayContaining([
        ['categories'],
        ['money-map'],
        ['transactions'],
        ['budget-analysis'],
        ['cash-flow'],
      ]),
    );
  });

  it('leaves the ledger alone on a rename, which moves no row', async () => {
    const { result, invalidatedKeys } = setup();

    await act(() => result.current.updateCategory.mutateAsync({ ...GROCERIES, name: 'Food' }));

    expect(invalidatedKeys()).toEqual(expect.arrayContaining([['categories'], ['money-map']]));
    expect(invalidatedKeys()).not.toContainEqual(['transactions']);
  });

  it('leaves the ledger alone on a create, which no row is in yet', async () => {
    const { result, invalidatedKeys } = setup();

    await act(() =>
      result.current.createCategory.mutateAsync({ userId: 1, name: 'Fun', categoryType: 'Expense' }),
    );

    expect(invalidatedKeys()).not.toContainEqual(['transactions']);
  });
});
