import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import api from '../../../shared/api';
import { CHASE } from '../../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import ConnectBankPrompt from './ConnectBankPrompt';

vi.mock('../../../shared/api', () => ({
  default: { get: vi.fn(), post: vi.fn(), delete: vi.fn() },
}));

// Plaid Link is a third-party iframe; a button stands in that completes the flow on click.
vi.mock('./PlaidLinkLauncher', () => ({
  default: function PlaidLinkLauncherMock({ onSuccess }: { onSuccess: (publicToken: string) => void }) {
    const [captured] = useState(() => onSuccess);
    return (
      <button type="button" onClick={() => captured('public-sandbox-token')}>
        Finish Plaid Link
      </button>
    );
  },
}));

const apiMock = vi.mocked(api, { deep: true });

const renderPrompt = () =>
  render(<ConnectBankPrompt description="Link a bank to fill this page." />, {
    wrapper: createQueryWrapper(createTestQueryClient()),
  });

describe('ConnectBankPrompt', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('asks someone with no bank linked to connect one', async () => {
    apiMock.get.mockResolvedValue({ data: [] });

    renderPrompt();

    expect(await screen.findByText('No bank linked yet')).toBeInTheDocument();
    expect(screen.getByText('Link a bank to fill this page.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Connect a bank' })).toBeInTheDocument();
  });

  it('stays out of the way once a bank is linked', async () => {
    apiMock.get.mockResolvedValue({ data: [CHASE] });

    renderPrompt();

    await waitFor(() => expect(apiMock.get).toHaveBeenCalledWith('/plaid/connections'));
    // Let the read settle before asserting nothing rendered.
    await new Promise((r) => setTimeout(r, 0));
    expect(screen.queryByText('No bank linked yet')).not.toBeInTheDocument();
  });

  it('says nothing while the connections read is failing, since nobody knows if a bank is linked', async () => {
    apiMock.get.mockRejectedValue(new Error('Network Error'));

    renderPrompt();

    // One retry, then the read is settled as an error.
    await waitFor(() => expect(apiMock.get).toHaveBeenCalledTimes(2), { timeout: 4000 });
    await new Promise((r) => setTimeout(r, 0));
    expect(screen.queryByText('No bank linked yet')).not.toBeInTheDocument();
  });

  it('opens Plaid Link in place and goes away once the bank is linked', async () => {
    const user = userEvent.setup();
    apiMock.get.mockResolvedValueOnce({ data: [] }).mockResolvedValue({ data: [CHASE] });
    apiMock.post.mockImplementation(async (url: string) => {
      if (url === '/plaid/link-token') return { data: { linkToken: 'link-token', expiration: '' } };
      return { data: { inserted: 3, updated: 0, removed: 0, syncedAt: '2026-11-28T12:00:00Z' } };
    });

    renderPrompt();

    await user.click(await screen.findByRole('button', { name: 'Connect a bank' }));
    await user.click(await screen.findByRole('button', { name: 'Finish Plaid Link' }));

    await waitFor(() =>
      expect(apiMock.post).toHaveBeenCalledWith('/plaid/exchange-token', { publicToken: 'public-sandbox-token' }),
    );
    await waitFor(() => expect(screen.queryByText('No bank linked yet')).not.toBeInTheDocument());
  });

  it('shows why linking could not start, with the button still there to try again', async () => {
    const user = userEvent.setup();
    apiMock.get.mockResolvedValue({ data: [] });
    apiMock.post.mockRejectedValue(new Error('500'));

    renderPrompt();

    await user.click(await screen.findByRole('button', { name: 'Connect a bank' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't start linking your bank. Please try again.");
    expect(screen.getByRole('button', { name: 'Connect a bank' })).toBeEnabled();
  });
});
