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

// Plaid Link is a third-party iframe; stand in a button that completes the flow on click.
vi.mock('./PlaidLinkLauncher', () => ({
  default: ({ onSuccess }: { onSuccess: (publicToken: string) => void }) => (
    <button type="button" onClick={() => onSuccess('public-sandbox-token')}>
      Finish Plaid Link
    </button>
  ),
}));

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
