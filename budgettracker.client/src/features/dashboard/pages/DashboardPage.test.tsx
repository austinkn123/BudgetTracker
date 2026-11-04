import { QueryClient } from '@tanstack/react-query';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { webullService } from '../../../shared/services/webull.service';
import type { PlaidConnectionView, PlanPerformance } from '../../../shared/types/api';
import { ALLY, CHASE, deferred } from '../../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { CASH_ACCOUNT, webullView } from '../../../test/webullFixtures';
import DashboardPage from './DashboardPage';

const plaid = vi.hoisted(() => ({ connections: [] as PlaidConnectionView[] }));
const analysis = vi.hoisted(() => ({ planMonth: null as PlanPerformance | null }));

// Only the connections matter here; everything else the dashboard reads is a settled, empty query.
vi.mock('../../linked-accounts/hooks/usePlaidConnections', () => ({
  usePlaidConnections: () => ({ data: plaid.connections }),
}));
vi.mock('../../linked-accounts/hooks/useSyncOnOpen', () => ({ useSyncOnOpen: () => undefined }));
vi.mock('../../user/hooks/useUser', () => ({ useUser: () => ({ isLoading: false, error: null }) }));
vi.mock('../../categories/hooks/useCategories', () => ({
  useCategories: () => ({ data: [], isLoading: false, error: null }),
}));
vi.mock('../../transactions/hooks/useTransactions', () => ({
  useTransactions: () => ({ data: [], isLoading: false, error: null }),
}));
vi.mock('../../../shared/services/webull.service', () => ({
  webullService: { get: vi.fn(), refresh: vi.fn(), verify: vi.fn(), getHoldings: vi.fn() },
}));
vi.mock('../hooks/useBudgetAnalysis', () => ({
  useBudgetAnalysis: () => ({
    data: { planMonth: analysis.planMonth, windowSpend: [], monthlyTrend: [] },
    isLoading: false,
    error: null,
  }),
}));

const getWebull = vi.mocked(webullService.get);
const refreshWebull = vi.mocked(webullService.refresh);
const verifyWebull = vi.mocked(webullService.verify);
const never = () => new Promise<never>(() => {});

/** Half way through June, exactly on pace: the hero reads "Right on the trail for June". */
const JUNE_PLAN: PlanPerformance = {
  plan: { id: 1, name: 'June plan', planMonth: '2026-06-01' },
  analyzedMonth: '2026-06-01',
  pacing: {
    daysElapsed: 15,
    daysInMonth: 30,
    daysPct: 0.5,
    spentPct: 0.5,
    projectedEnd: 4000,
    pacingDelta: 0,
    status: 'OnTrack',
    plannedExpenses: 4000,
    actualExpenses: 2000,
    remaining: 2000,
    perDiemToStay: 133.33,
  },
  byCategory: [],
  byBucket: [],
  income: 5000,
  expenses: 2000,
};

/** The same account after a refresh moved its value, so old and new figures are distinguishable. */
const REFRESHED = webullView({
  balances: [{ ...CASH_ACCOUNT, totalNetLiquidationValue: 13000 }],
  asOf: '2026-10-22T19:00:00Z',
});

const renderDashboard = (queryClient: QueryClient = createTestQueryClient(), strict = false) => {
  const Wrapper = createQueryWrapper(queryClient);
  return render(
    <MemoryRouter>
      <Wrapper>
        <DashboardPage />
      </Wrapper>
    </MemoryRouter>,
    { reactStrictMode: strict },
  );
};

beforeEach(() => {
  // Webull stays in flight unless a test says otherwise, so it never touches the alerts below.
  getWebull.mockReset().mockImplementation(never);
  refreshWebull.mockReset().mockImplementation(never);
  verifyWebull.mockReset();
  analysis.planMonth = null;
});

describe('DashboardPage sign-in alert', () => {
  beforeEach(() => {
    plaid.connections = [];
  });

  it('names every bank that needs sign-in and links to Settings', () => {
    plaid.connections = [
      { ...CHASE, needsReconnect: true },
      { ...ALLY, needsReconnect: true },
    ];

    renderDashboard();

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Chase and Ally Bank need you to sign in again');
    expect(within(alert).getByRole('link', { name: 'Reconnect in Settings' })).toHaveAttribute(
      'href',
      '/settings',
    );
  });

  it('uses "needs" for a single bank and leaves the healthy one out', () => {
    plaid.connections = [{ ...CHASE, needsReconnect: true }, ALLY];

    renderDashboard();

    expect(screen.getByRole('alert')).toHaveTextContent('Chase needs you to sign in again');
    expect(screen.getByRole('alert')).not.toHaveTextContent('Ally Bank');
  });

  it('shows no alert when no bank needs sign-in', () => {
    plaid.connections = [CHASE, ALLY];

    renderDashboard();

    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});

describe('DashboardPage Webull refresh on open', () => {
  // The net worth card repeats the Webull total, so each amount can appear twice.
  it('refreshes once per mount, stores the view, and touches no budget query', async () => {
    const view = webullView();
    refreshWebull.mockResolvedValue(view);
    const queryClient = createTestQueryClient();
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    renderDashboard(queryClient, true);

    expect((await screen.findAllByText('$12,345.67'))[0]).toBeInTheDocument();
    expect(refreshWebull).toHaveBeenCalledTimes(1);
    expect(queryClient.getQueryData(['webull'])).toEqual(view);
    // Holdings are re-read after a refresh; no budget query is ever touched.
    await waitFor(() => expect(invalidate).toHaveBeenCalled());
    expect(invalidate.mock.calls.map(([filters]) => filters?.queryKey)).toEqual([
      ['webull-holdings'],
    ]);
    expect(verifyWebull).not.toHaveBeenCalled();
  });

  it('shows the stored balance while the background refresh is in flight, then the refreshed one', async () => {
    getWebull.mockResolvedValue(webullView());
    const refresh = deferred<typeof REFRESHED>();
    refreshWebull.mockReturnValue(refresh.promise);

    renderDashboard();

    expect((await screen.findAllByText('$12,345.67'))[0]).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();

    refresh.resolve(REFRESHED);

    expect((await screen.findAllByText('$13,000.00'))[0]).toBeInTheDocument();
    expect(screen.queryByText('$12,345.67')).not.toBeInTheDocument();
  });

  it('a GET that lands after the refresh does not overwrite the newer view', async () => {
    const get = deferred<ReturnType<typeof webullView>>();
    getWebull.mockReturnValue(get.promise);
    refreshWebull.mockResolvedValue(REFRESHED);
    const queryClient = createTestQueryClient();

    renderDashboard(queryClient);

    expect((await screen.findAllByText('$13,000.00'))[0]).toBeInTheDocument();

    // The GET was sent first but answers last, with the older balance.
    await act(async () => get.resolve(webullView()));

    expect(queryClient.getQueryData(['webull'])).toEqual(REFRESHED);
    expect(screen.getAllByText('$13,000.00')[0]).toBeInTheDocument();
    expect(screen.queryByText('$12,345.67')).not.toBeInTheDocument();
  });

  it('keeps the dashboard up, silently, when Webull fails', async () => {
    getWebull.mockRejectedValue(new Error('500'));
    refreshWebull.mockRejectedValue(new Error('502'));
    // Client default says "retry 3 times"; the refresh must override it with retry: false.
    const retryingClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: 3, retryDelay: 0 } },
    });

    renderDashboard(retryingClient);

    await waitFor(() =>
      expect(retryingClient.getMutationCache().getAll()[0]?.state.status).toBe('error'),
    );
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    expect(refreshWebull).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.queryByText("Couldn't load your dashboard")).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByText(/Webull/)).not.toBeInTheDocument();
    expect(verifyWebull).not.toHaveBeenCalled();
  });
});

describe('DashboardPage Webull card placement', () => {
  it('puts the Webull card above the plan hero, with and without a plan', async () => {
    getWebull.mockResolvedValue(webullView());

    const cases: [PlanPerformance | null, string][] = [
      [null, 'Make a plan and this becomes your month'],
      [JUNE_PLAN, 'Right on the trail for June'],
    ];

    for (const [planMonth, heroHeading] of cases) {
      analysis.planMonth = planMonth;
      const { unmount } = renderDashboard();

      const card = await screen.findByRole('heading', { name: 'Webull' });
      const anchor = screen.getByRole('heading', { name: heroHeading });
      expect(card.compareDocumentPosition(anchor) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

      unmount();
    }
  });
});
