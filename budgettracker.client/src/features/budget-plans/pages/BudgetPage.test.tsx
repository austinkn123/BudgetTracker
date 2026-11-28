import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { PlaidConnectionView } from '../../../shared/types/api';
import { CHASE } from '../../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import BudgetPage from './BudgetPage';

const map = vi.hoisted(() => ({ refetch: vi.fn(), isError: true }));
const plaid = vi.hoisted(() => ({ connections: [] as PlaidConnectionView[] }));

vi.mock('../../money-map/hooks/useMoneyMap', () => ({
  useMoneyMap: () => ({
    data: undefined,
    isPending: false,
    isFetching: false,
    error: map.isError ? new Error('500') : null,
    refetch: map.refetch,
  }),
}));
vi.mock('../../linked-accounts/hooks/usePlaidConnections', () => ({
  PLAID_CONNECTIONS_QUERY_KEY: ['plaid', 'connections'],
  usePlaidConnections: () => ({ data: plaid.connections, isSuccess: true }),
}));
vi.mock('../PlanTab', () => ({ default: () => <p>plan tab</p> }));

const renderPage = () => {
  const Wrapper = createQueryWrapper(createTestQueryClient());
  render(
    <Wrapper>
      <MemoryRouter>
        <BudgetPage />
      </MemoryRouter>
    </Wrapper>,
  );
};

describe('BudgetPage', () => {
  beforeEach(() => {
    map.refetch = vi.fn();
    map.isError = true;
    plaid.connections = [CHASE];
  });

  it('offers Try again when the budget fails to load', async () => {
    renderPage();

    expect(screen.getByRole('alert')).toHaveTextContent("Couldn't load your budget");
    expect(screen.queryByText(/server and database/)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(map.refetch).toHaveBeenCalledOnce();
  });

  it('asks someone with no bank linked to connect one', () => {
    plaid.connections = [];
    renderPage();

    expect(screen.getByText('No bank linked yet')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Connect a bank' })).toBeInTheDocument();
  });

  it('does not ask once a bank is linked', () => {
    renderPage();

    expect(screen.queryByText('No bank linked yet')).not.toBeInTheDocument();
  });
});
