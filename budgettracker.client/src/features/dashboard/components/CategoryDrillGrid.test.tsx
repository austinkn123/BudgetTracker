import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { transactionService } from '../../../shared/services/transaction.service';
import type { Transaction } from '../../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import type { CategoryCard } from '../utils/selectors';
import CategoryDrillGrid from './CategoryDrillGrid';

vi.mock('../../../shared/services/transaction.service', () => ({
  transactionService: { getFiltered: vi.fn(), getPage: vi.fn() },
}));

const getPage = vi.mocked(transactionService.getPage);

const GROCERIES: CategoryCard = {
  key: '4',
  categoryId: 4,
  name: 'Groceries',
  planned: 400,
  actual: 120,
  monthly: [],
};

const RENT: CategoryCard = { ...GROCERIES, key: '7', categoryId: 7, name: 'Rent' };

const txn = (id: number, payee: string): Transaction => ({
  id,
  accountId: 1,
  transactionType: 'Expense',
  categoryId: 4,
  payee,
  amount: -42,
  occurredAt: '2024-02-03T00:00:00Z',
  createdAt: '2024-02-03T00:00:00Z',
});

const renderGrid = (rangeFilter = {}) => {
  const Wrapper = createQueryWrapper(createTestQueryClient());
  return render(
    <Wrapper>
      <CategoryDrillGrid cards={[GROCERIES, RENT]} rangeFilter={rangeFilter} transactionLimit={10} />
    </Wrapper>,
  );
};

beforeEach(() => {
  getPage.mockReset().mockResolvedValue({ items: [], nextCursor: null });
});

describe('CategoryDrillGrid', () => {
  it('fetches no rows until a category is opened', () => {
    renderGrid();

    expect(getPage).not.toHaveBeenCalled();
  });

  it('reads the opened category\'s own newest rows, however old', async () => {
    // A quiet category: its last purchase is long behind the newest rows of the ledger.
    getPage.mockResolvedValue({ items: [txn(1, 'Corner Grocer')], nextCursor: null });
    renderGrid();

    fireEvent.click(screen.getByRole('button', { name: 'Show transactions for Groceries' }));

    expect(await screen.findByText('Corner Grocer')).toBeInTheDocument();
    expect(getPage).toHaveBeenCalledTimes(1);
    expect(getPage).toHaveBeenCalledWith({ categoryId: 4 }, { limit: 10 });
  });

  it('keeps a bounded range on the drill read', async () => {
    renderGrid({ from: '2026-09-01', to: '2026-11-30' });

    fireEvent.click(screen.getByRole('button', { name: 'Show transactions for Rent' }));

    expect(await screen.findByText('No transactions in this range.')).toBeInTheDocument();
    expect(getPage).toHaveBeenCalledWith(
      { from: '2026-09-01', to: '2026-11-30', categoryId: 7 },
      { limit: 10 },
    );
  });

  it('says so, without leaking the error, when the rows fail to load', async () => {
    getPage.mockRejectedValue(new Error('Request failed with status code 500'));
    renderGrid();

    fireEvent.click(screen.getByRole('button', { name: 'Show transactions for Groceries' }));

    expect(await screen.findByText("Couldn't load these transactions.", {}, { timeout: 3000 })).toBeInTheDocument();
    expect(screen.queryByText(/status code/)).not.toBeInTheDocument();
  });
});
