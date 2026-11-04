import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { format, startOfMonth } from 'date-fns';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { CashFlowReport } from '../../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { CHASE } from '../../../test/plaidFixtures';
import CashFlowPage from './CashFlowPage';

const api = vi.hoisted(() => ({ getReport: vi.fn() }));

vi.mock('../../../shared/services/cashFlow.service', () => ({
  cashFlowService: { getReport: api.getReport },
}));
vi.mock('../../linked-accounts/hooks/usePlaidConnections', () => ({
  usePlaidConnections: () => ({ data: [CHASE] }),
}));
vi.mock('../../linked-accounts/hooks/useSyncOnOpen', () => ({ useSyncOnOpen: () => undefined }));
vi.mock('../../categories/hooks/useCategories', () => ({
  useCategories: () => ({
    data: [{ id: 3, userId: 1, name: 'Groceries', categoryType: 'Expense' }],
    isLoading: false,
    error: null,
  }),
}));

// jsdom has no layout engine; Recharts' ResponsiveContainer only needs the constructor to exist.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
};

const report = (over: Partial<CashFlowReport> = {}): CashFlowReport => ({
  periods: [
    { start: '2026-10-01', moneyIn: 5000, moneyOut: 1200, net: 3800, balance: 9000 },
    { start: '2026-10-02', moneyIn: 0, moneyOut: 300, net: -300, balance: 8700 },
  ],
  totals: { moneyIn: 5000, moneyOut: 1500, net: 3500 },
  previousTotals: { moneyIn: 4000, moneyOut: 2000, net: 2000 },
  pending: { moneyIn: 0, moneyOut: 42.5, net: -42.5 },
  incomeSources: [{ source: 'Gusto', amount: 5000 }],
  spendByCategory: [{ categoryId: 3, amount: 1500 }],
  currentBalance: 8700,
  ...over,
});

const renderPage = () =>
  render(
    <MemoryRouter>
      <CashFlowPage />
    </MemoryRouter>,
    { wrapper: createQueryWrapper(createTestQueryClient()) },
  );

const tile = (label: string) => screen.getByText(label, { selector: 'dt' }).parentElement!;

describe('CashFlowPage', () => {
  beforeEach(() => {
    window.localStorage.clear();
    api.getReport.mockReset();
  });

  it('asks for this month by day, compared with last month, by default', async () => {
    api.getReport.mockResolvedValue(report());
    renderPage();

    await screen.findByText('Money in', { selector: 'dt' });
    expect(api.getReport).toHaveBeenCalledWith(
      expect.objectContaining({
        from: format(startOfMonth(new Date()), 'yyyy-MM-dd'),
        to: format(new Date(), 'yyyy-MM-dd'),
        groupBy: 'Day',
        compareFrom: expect.any(String),
        compareTo: expect.any(String),
      }),
    );
  });

  it('shows the totals, the change against the previous period, and pending money', async () => {
    api.getReport.mockResolvedValue(report());
    renderPage();

    await screen.findByText('Money in', { selector: 'dt' });
    expect(within(tile('Money in')).getByText('$5,000', { selector: '.sr-only' })).toBeInTheDocument();
    expect(tile('Money in')).toHaveTextContent('Up 25% vs previous period');
    expect(tile('Money out')).toHaveTextContent('Down 25% vs previous period');
    expect(tile('Money out')).toHaveTextContent('+$42.50 pending');
    expect(within(tile('Net')).getByText('+$3,500', { selector: '.sr-only' })).toBeInTheDocument();
    // 70% now against 50% before: a rate moves in points.
    expect(within(tile('Savings rate')).getByText('70%', { selector: '.sr-only' })).toBeInTheDocument();
    expect(tile('Savings rate')).toHaveTextContent('Up 20 pts vs previous period');

    expect(screen.getByText('Where it came from')).toBeInTheDocument();
    expect(screen.getByText('Gusto')).toBeInTheDocument();
    expect(screen.getByText('Where it went')).toBeInTheDocument();
    expect(screen.getByText('Groceries')).toBeInTheDocument();
    expect(screen.getByText('Balance')).toBeInTheDocument();
  });

  it('shows a dash for savings rate and hides the balance when they have no meaning', async () => {
    api.getReport.mockResolvedValue(
      report({
        totals: { moneyIn: 0, moneyOut: 80, net: -80 },
        previousTotals: null,
        currentBalance: null,
        periods: [{ start: '2026-10-01', moneyIn: 0, moneyOut: 80, net: -80, balance: null }],
      }),
    );
    renderPage();

    await screen.findByText('Money in', { selector: 'dt' });
    expect(within(tile('Savings rate')).getByText('—', { selector: '.sr-only' })).toBeInTheDocument();
    expect(tile('Money in')).not.toHaveTextContent('vs previous period');
    expect(screen.queryByText('Balance')).not.toBeInTheDocument();
  });

  it('signs pending money from its value, so pending refunds read as less money out', async () => {
    api.getReport.mockResolvedValue(report({ pending: { moneyIn: 0, moneyOut: -20, net: 20 } }));
    renderPage();

    await screen.findByText('Money in', { selector: 'dt' });
    expect(tile('Money out')).toHaveTextContent('-$20.00 pending');
    expect(tile('Money out')).not.toHaveTextContent('+-');
  });

  it('totals "Where it went" to the Money out figure, even when refunds push a category negative', async () => {
    api.getReport.mockResolvedValue(
      report({
        totals: { moneyIn: 5000, moneyOut: 80, net: 4920 },
        // Category 4's refunds outweigh its spend; the ranked list drops it, the total must not.
        spendByCategory: [
          { categoryId: 3, amount: 100 },
          { categoryId: 4, amount: -20 },
        ],
      }),
    );
    renderPage();

    await screen.findByText('Where it went');
    expect(screen.getByText('$80 total')).toBeInTheDocument();
    expect(screen.queryByText('$100 total')).not.toBeInTheDocument();
  });

  it('says so when nothing moved', async () => {
    api.getReport.mockResolvedValue(
      report({
        periods: [{ start: '2026-10-01', moneyIn: 0, moneyOut: 0, net: 0, balance: null }],
        totals: { moneyIn: 0, moneyOut: 0, net: 0 },
        incomeSources: [],
        spendByCategory: [],
        currentBalance: null,
      }),
    );
    renderPage();

    expect(await screen.findByText('No money moved in this range')).toBeInTheDocument();
  });

  it('reopens with the persisted range and its default grouping', async () => {
    window.localStorage.setItem('bt:cash-flow:controls', JSON.stringify({ range: 'ytd' }));
    api.getReport.mockResolvedValue(report());
    renderPage();

    await screen.findByText('Money in', { selector: 'dt' });
    expect(api.getReport).toHaveBeenCalledWith(
      expect.objectContaining({ from: format(new Date(), 'yyyy-01-01'), groupBy: 'Month' }),
    );
    expect(screen.getByRole('radio', { name: 'YTD' })).toHaveAttribute('data-state', 'on');
  });

  it('keeps the controls up and explains when the report fails', async () => {
    api.getReport.mockRejectedValue(new Error('Network Error'));
    renderPage();

    expect(
      await screen.findByText("Couldn't load your cash flow", {}, { timeout: 4000 }),
    ).toBeInTheDocument();
    expect(screen.getByRole('radiogroup', { name: 'Date range' })).toBeInTheDocument();
  });
});
