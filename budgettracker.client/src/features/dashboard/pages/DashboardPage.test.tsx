import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { PlaidConnectionView } from '../../../shared/types/api';
import { ALLY, CHASE } from '../../../test/plaidFixtures';
import DashboardPage from './DashboardPage';

const plaid = vi.hoisted(() => ({ connections: [] as PlaidConnectionView[] }));

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
vi.mock('../hooks/useBudgetAnalysis', () => ({
  useBudgetAnalysis: () => ({
    data: { planMonth: null, windowSpend: [], monthlyTrend: [] },
    isLoading: false,
    error: null,
  }),
}));

const renderDashboard = () =>
  render(
    <MemoryRouter>
      <DashboardPage />
    </MemoryRouter>,
  );

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
