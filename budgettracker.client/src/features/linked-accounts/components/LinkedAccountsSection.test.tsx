import { useState } from 'react';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import api from '../../../shared/api';
import { ALLY, ALREADY_LINKED_MESSAGE, CHASE, axiosBadRequest } from '../../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import LinkedAccountsSection from './LinkedAccountsSection';

vi.mock('../../../shared/api', () => ({
  default: { get: vi.fn(), post: vi.fn(), delete: vi.fn() },
}));

// Plaid Link is a third-party iframe; stand in a button that completes the flow on click. Like
// react-plaid-link 4.x, it keeps the onSuccess it was mounted with (the real one freezes it when
// the token is set), so a handler that only becomes right on a later render fails here too.
vi.mock('./PlaidLinkLauncher', () => ({
  default: function PlaidLinkLauncherMock({
    onSuccess,
  }: {
    onSuccess: (publicToken: string) => void;
  }) {
    const [captured] = useState(() => onSuccess);
    return (
      <button type="button" onClick={() => captured('public-sandbox-token')}>
        Finish Plaid Link
      </button>
    );
  },
}));

const LOGIN_REQUIRED_MESSAGE =
  'A bank needs you to sign in again. Open Settings and click Reconnect.';

const apiMock = vi.mocked(api, { deep: true });

const renderSection = () => {
  const Wrapper = createQueryWrapper(createTestQueryClient());
  return render(
    <Wrapper>
      <LinkedAccountsSection />
    </Wrapper>,
  );
};

describe('LinkedAccountsSection', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('renders one card per connection', async () => {
    apiMock.get.mockResolvedValue({ data: [CHASE, ALLY] });

    renderSection();

    const list = await screen.findByRole('list', { name: 'Linked institutions' });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByText('Chase')).toBeInTheDocument();
    expect(within(items[1]).getByText('Ally Bank')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Disconnect Chase' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Disconnect Ally Bank' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Connect a bank' })).not.toBeInTheDocument();
  });

  it('flags a bank that needs sign-in, and Reconnect re-syncs it without exchanging', async () => {
    const user = userEvent.setup();
    apiMock.get.mockResolvedValue({ data: [{ ...CHASE, needsReconnect: true }, ALLY] });
    apiMock.post.mockImplementation(async (url: string) => {
      if (url === `/plaid/connections/${CHASE.plaidItemId}/link-token`) {
        return { data: { linkToken: 'link-update-token', expiration: '' } };
      }
      return { data: { inserted: 0, updated: 0, removed: 0, syncedAt: '2026-10-10T12:00:00Z' } };
    });

    renderSection();

    const list = await screen.findByRole('list', { name: 'Linked institutions' });
    const [chase, ally] = within(list).getAllByRole('listitem');
    expect(within(chase).getByText('Needs sign-in')).toBeInTheDocument();
    expect(within(chase).queryByText('Active')).not.toBeInTheDocument();
    expect(within(chase).getByText(/Syncing is paused until you sign in again\./)).toBeInTheDocument();
    expect(within(ally).getByText('Active')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reconnect Ally Bank' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Reconnect Chase' }));
    await user.click(await screen.findByRole('button', { name: 'Finish Plaid Link' }));

    await waitFor(() =>
      expect(apiMock.post.mock.calls.map(([url]) => url)).toEqual([
        `/plaid/connections/${CHASE.plaidItemId}/link-token`,
        `/plaid/connections/${CHASE.plaidItemId}/reconnected`,
        '/plaid/sync',
      ]),
    );
  });

  it('refetches connections when Refresh all hits a bank that needs sign-in, so Reconnect shows', async () => {
    const user = userEvent.setup();
    // The failed sync flags Chase server-side; only a refetch can show it.
    apiMock.get
      .mockResolvedValueOnce({ data: [CHASE, ALLY] })
      .mockResolvedValue({ data: [{ ...CHASE, needsReconnect: true }, ALLY] });
    apiMock.post.mockRejectedValue(axiosBadRequest(LOGIN_REQUIRED_MESSAGE));

    renderSection();

    await user.click(await screen.findByRole('button', { name: 'Refresh all' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(LOGIN_REQUIRED_MESSAGE);
    expect(await screen.findByRole('button', { name: 'Reconnect Chase' })).toBeInTheDocument();
    expect(screen.getByText('Needs sign-in')).toBeInTheDocument();
  });

  it('shows "Connect a bank" when nothing is linked', async () => {
    apiMock.get.mockResolvedValue({ data: [] });

    renderSection();

    expect(await screen.findByRole('button', { name: 'Connect a bank' })).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: 'Linked institutions' })).not.toBeInTheDocument();
  });

  it('names the institution in the confirm modal, and cancel does not disconnect', async () => {
    const user = userEvent.setup();
    apiMock.get.mockResolvedValue({ data: [CHASE, ALLY] });

    renderSection();

    await user.click(await screen.findByRole('button', { name: 'Disconnect Ally Bank' }));

    const dialog = await screen.findByRole('dialog', { name: 'Disconnect Ally Bank?' });
    expect(within(dialog).getByText('Ally Bank', { selector: 'strong' })).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(apiMock.delete).not.toHaveBeenCalled();
  });

  it('shows the server message when re-linking an already-linked bank, and it can be dismissed', async () => {
    const user = userEvent.setup();
    apiMock.get.mockResolvedValue({ data: [CHASE] });
    apiMock.post.mockImplementation(async (url: string) => {
      if (url === '/plaid/link-token') return { data: { linkToken: 'link-sandbox-token' } };
      throw axiosBadRequest(ALREADY_LINKED_MESSAGE);
    });

    renderSection();

    await user.click(await screen.findByRole('button', { name: 'Add institution' }));
    await user.click(await screen.findByRole('button', { name: 'Finish Plaid Link' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(ALREADY_LINKED_MESSAGE);
    // The existing connection is untouched and linking can be retried.
    expect(screen.getByText('Chase')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add institution' })).toBeEnabled();

    await user.click(within(alert).getByRole('button', { name: 'Dismiss' }));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
