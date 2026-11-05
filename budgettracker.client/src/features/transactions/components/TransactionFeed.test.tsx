import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Transaction } from '../../../shared/types/api';
import TransactionFeed, { type TransactionFeedProps } from './TransactionFeed';

/** A stand-in IntersectionObserver the test can drive: `reveal()` reports the sentinel as visible. */
const observers: { callback: IntersectionObserverCallback; targets: Element[] }[] = [];

class FakeIntersectionObserver {
  private readonly entry: { callback: IntersectionObserverCallback; targets: Element[] };
  constructor(callback: IntersectionObserverCallback) {
    this.entry = { callback, targets: [] };
    observers.push(this.entry);
  }
  observe(target: Element) {
    this.entry.targets.push(target);
  }
  disconnect() {
    observers.splice(observers.indexOf(this.entry), 1);
  }
  unobserve() {}
  takeRecords() {
    return [];
  }
}

const reveal = () =>
  act(() => {
    for (const { callback, targets } of [...observers]) {
      callback(
        targets.map((target) => ({ isIntersecting: true, target }) as IntersectionObserverEntry),
        {} as IntersectionObserver,
      );
    }
  });

const tx = (id: number): Transaction => ({
  id,
  accountId: 1,
  transactionType: 'Expense',
  categoryId: null,
  amount: -10,
  occurredAt: '2026-10-15T00:00:00',
  createdAt: '2026-10-15T00:00:00',
  payee: `Payee ${id}`,
});

const renderFeed = (props: Partial<TransactionFeedProps> = {}) => {
  const fetchNextPage = vi.fn();
  const onToggleAll = vi.fn();
  render(
    <TransactionFeed
      transactions={[tx(1), tx(2)]}
      isLoading={false}
      isError={false}
      hasNextPage
      isFetchingNextPage={false}
      isFetchNextPageError={false}
      fetchNextPage={fetchNextPage}
      onRetry={vi.fn()}
      allSelected={false}
      onToggleAll={onToggleAll}
      isFiltered={false}
      rowProps={{
        categories: [],
        maskByPlaidAccountId: new Map(),
        selectedIds: new Set(),
        onToggleSelected: vi.fn(),
        onCategoryChange: vi.fn(),
        onNotesChange: vi.fn(),
        isBusy: false,
      }}
      {...props}
    />,
  );
  return { fetchNextPage, onToggleAll };
};

beforeEach(() => {
  vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver);
});

afterEach(() => {
  vi.unstubAllGlobals();
  observers.length = 0;
});

describe('TransactionFeed', () => {
  it('loads the next page when the bottom of the list scrolls into view', () => {
    const { fetchNextPage } = renderFeed();

    expect(fetchNextPage).not.toHaveBeenCalled();
    reveal();

    expect(fetchNextPage).toHaveBeenCalledTimes(1);
  });

  it('does not ask again while a page is already on its way', () => {
    const { fetchNextPage } = renderFeed({ isFetchingNextPage: true });

    reveal();

    expect(fetchNextPage).not.toHaveBeenCalled();
    expect(screen.getByRole('status')).toHaveTextContent('Loading older transactions…');
  });

  it('offers a button too, for anyone not scrolling', () => {
    const { fetchNextPage } = renderFeed();

    fireEvent.click(screen.getByRole('button', { name: 'Load older transactions' }));

    expect(fetchNextPage).toHaveBeenCalledTimes(1);
  });

  it('says how many are loaded while more remain, and selects only those', () => {
    const { onToggleAll } = renderFeed();

    expect(screen.getByText('2 loaded')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Select loaded' }));
    expect(onToggleAll).toHaveBeenCalledWith(true);
  });

  it('reports the full count and the end of history once every page is in', () => {
    const { fetchNextPage } = renderFeed({ hasNextPage: false });

    reveal();

    expect(fetchNextPage).not.toHaveBeenCalled();
    expect(screen.getByText('2 transactions')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('the start of your history');
    expect(screen.getByRole('button', { name: 'Select all' })).toBeInTheDocument();
  });

  it('words the end differently when chips or search narrow the feed', () => {
    renderFeed({ hasNextPage: false, isFiltered: true });

    expect(screen.getByRole('status')).toHaveTextContent('No older transactions match this view.');
  });

  it('keeps loaded rows and offers a retry when a later page fails', () => {
    const onRetry = vi.fn();
    renderFeed({ isFetchNextPageError: true, onRetry });

    expect(screen.getByText('Payee 1')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('shows skeleton rows before the first page arrives', () => {
    renderFeed({ transactions: [], isLoading: true });

    expect(screen.getByLabelText('Loading transactions')).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByRole('button', { name: 'Load older transactions' })).not.toBeInTheDocument();
  });
});
