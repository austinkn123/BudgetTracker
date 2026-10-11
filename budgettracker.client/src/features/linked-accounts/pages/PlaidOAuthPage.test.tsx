import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import api from '../../../shared/api';
import { ALREADY_LINKED_MESSAGE, CHASE, axiosBadRequest } from '../../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { loadPlaidLink, savePlaidLink } from '../hooks/useLinkedAccounts';
import PlaidOAuthPage from './PlaidOAuthPage';

vi.mock('../../../shared/api', () => ({
  default: { get: vi.fn(), post: vi.fn(), delete: vi.fn() },
}));

// Plaid Link is a third-party iframe; stand in buttons that finish or exit the resumed flow.
vi.mock('../components/PlaidLinkLauncher', () => ({
  default: ({
    onSuccess,
    onExit,
  }: {
    onSuccess: (publicToken: string) => void;
    onExit: () => void;
  }) => (
    <>
      <button type="button" onClick={() => onSuccess('public-oauth-token')}>
        Finish Plaid Link
      </button>
      <button type="button" onClick={onExit}>
        Exit Plaid Link
      </button>
    </>
  ),
}));

const apiMock = vi.mocked(api, { deep: true });

const renderOAuthReturn = () => {
  const Wrapper = createQueryWrapper(createTestQueryClient());
  return render(
    <Wrapper>
      <MemoryRouter initialEntries={['/plaid-oauth?oauth_state_id=state-123']}>
        <Routes>
          <Route path="/plaid-oauth" element={<PlaidOAuthPage />} />
          <Route path="/settings" element={<p>Settings page</p>} />
        </Routes>
      </MemoryRouter>
    </Wrapper>,
  );
};

describe('PlaidOAuthPage', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    localStorage.clear();
    apiMock.get.mockResolvedValue({ data: [CHASE] });
  });

  it('goes to Settings when there is no link session to resume', async () => {
    renderOAuthReturn();

    expect(await screen.findByText('Settings page')).toBeInTheDocument();
    expect(apiMock.post).not.toHaveBeenCalled();
  });

  it('resumes the saved session, exchanges the token, and lands on Settings', async () => {
    const user = userEvent.setup();
    savePlaidLink({ linkToken: 'link-sandbox-token', reconnectItemId: null });
    apiMock.post.mockResolvedValue({
      data: { inserted: 1, updated: 0, removed: 0, syncedAt: '2026-10-10T12:00:00Z' },
    });

    renderOAuthReturn();

    expect(screen.getByText('Finishing your bank connection…')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Finish Plaid Link' }));

    expect(await screen.findByText('Settings page')).toBeInTheDocument();
    expect(apiMock.post).toHaveBeenCalledWith('/plaid/exchange-token', {
      publicToken: 'public-oauth-token',
    });
    expect(loadPlaidLink()).toBeNull();
  });

  it('resumes a reconnect session: marks it reconnected and syncs, never exchanges', async () => {
    const user = userEvent.setup();
    savePlaidLink({ linkToken: 'link-update-token', reconnectItemId: CHASE.plaidItemId });
    apiMock.post.mockResolvedValue({
      data: { inserted: 0, updated: 0, removed: 0, syncedAt: '2026-10-10T12:00:00Z' },
    });

    renderOAuthReturn();

    await user.click(screen.getByRole('button', { name: 'Finish Plaid Link' }));

    expect(await screen.findByText('Settings page')).toBeInTheDocument();
    expect(apiMock.post.mock.calls.map(([url]) => url)).toEqual([
      `/plaid/connections/${CHASE.plaidItemId}/reconnected`,
      '/plaid/sync',
    ]);
    expect(loadPlaidLink()).toBeNull();
  });

  it('forgets the saved session and returns to Settings on exit', async () => {
    const user = userEvent.setup();
    savePlaidLink({ linkToken: 'link-sandbox-token', reconnectItemId: null });

    renderOAuthReturn();

    await user.click(screen.getByRole('button', { name: 'Exit Plaid Link' }));

    expect(await screen.findByText('Settings page')).toBeInTheDocument();
    expect(loadPlaidLink()).toBeNull();
    expect(apiMock.post).not.toHaveBeenCalled();
  });

  it('shows the error with a way back to Settings when the exchange fails', async () => {
    const user = userEvent.setup();
    savePlaidLink({ linkToken: 'link-sandbox-token', reconnectItemId: null });
    apiMock.post.mockRejectedValue(axiosBadRequest(ALREADY_LINKED_MESSAGE));

    renderOAuthReturn();

    await user.click(screen.getByRole('button', { name: 'Finish Plaid Link' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(ALREADY_LINKED_MESSAGE);
    expect(within(alert).getByRole('link', { name: 'Back to Settings' })).toHaveAttribute(
      'href',
      '/settings',
    );
    expect(screen.queryByText('Settings page')).not.toBeInTheDocument();
  });
});
