import { render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { webullService } from '../../../shared/services/webull.service';
import type { WebullConnectionView, WebullState } from '../../../shared/types/api';
import { localDateTime } from '../../../shared/utils/format';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { CASH_ACCOUNT, MARGIN_ACCOUNT, webullView } from '../../../test/webullFixtures';
import WebullBalanceCard from './WebullBalanceCard';

vi.mock('../../../shared/services/webull.service', () => ({
  webullService: { get: vi.fn(), refresh: vi.fn(), verify: vi.fn() },
}));

const getMock = vi.mocked(webullService.get);

const VERIFY_MESSAGE =
  'Webull needs you to verify in the Webull app before the balance can update';
const UNREACHABLE_NOTE = "Couldn't reach Webull. Showing the last known balance.";
const UNAVAILABLE = 'Webull balance unavailable right now';
/** Anything that could take a key, secret or token, including `<input type="password">`. */
const ANY_INPUT = 'input, textarea, select, [contenteditable="true"]';

const renderCard = () => {
  const Wrapper = createQueryWrapper(createTestQueryClient());
  return render(
    <MemoryRouter>
      <Wrapper>
        <WebullBalanceCard />
      </Wrapper>
    </MemoryRouter>,
  );
};

/** Renders the card once the GET has resolved to `view`. */
const renderWith = async (view: WebullConnectionView) => {
  getMock.mockResolvedValue(view);
  const result = renderCard();
  await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
  return result;
};

describe('WebullBalanceCard', () => {
  beforeEach(() => {
    getMock.mockReset();
  });

  it('shows a loading placeholder, not zeros, before the first data arrives', () => {
    getMock.mockReturnValue(new Promise(() => {}));

    renderCard();

    expect(screen.getByRole('status')).toHaveTextContent('Loading your Webull balance');
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
  });

  it('renders nothing at all when Webull is not set up', async () => {
    const { container } = await renderWith(
      webullView({ state: 'NotSetUp', balances: [], asOf: null }),
    );

    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing when the request itself fails, so the rest of the page is untouched', async () => {
    getMock.mockRejectedValue(new Error('500'));

    const { container } = renderCard();

    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });

  it('shows one row per account in its own currency, with no combined total', async () => {
    const { container } = await renderWith(
      webullView({ balances: [CASH_ACCOUNT, MARGIN_ACCOUNT] }),
    );

    const rows = within(screen.getByRole('list', { name: 'Webull accounts' })).getAllByRole(
      'listitem',
    );
    expect(rows).toHaveLength(2);

    expect(within(rows[0]).getByText('Individual Cash')).toBeInTheDocument();
    expect(within(rows[0]).getByText('Total value')).toBeInTheDocument();
    expect(within(rows[0]).getByText('$12,345.67')).toBeInTheDocument();
    expect(within(rows[0]).getByText('Cash')).toBeInTheDocument();
    expect(within(rows[0]).getByText('$500.00')).toBeInTheDocument();

    // No label: falls back to the account type. No cash figure: a dash, never $0.
    expect(within(rows[1]).getByText('MARGIN')).toBeInTheDocument();
    expect(within(rows[1]).getByText('CA$2,000.00')).toBeInTheDocument();
    expect(within(rows[1]).getByText('—')).toBeInTheDocument();

    // 12,345.67 + 2,000: summing across accounts (and currencies) would be meaningless.
    expect(screen.queryByText('$14,345.67')).not.toBeInTheDocument();
    expect(screen.getAllByText('Total value')).toHaveLength(2);

    expect(screen.getByText(`As of ${localDateTime('2026-10-22T18:04:11Z')}`)).toBeInTheDocument();
    expect(screen.queryByText(VERIFY_MESSAGE)).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(container.querySelector(ANY_INPUT)).toBeNull();
  });

  it('links to the holdings page', async () => {
    await renderWith(webullView());

    expect(screen.getByRole('link', { name: 'View holdings' })).toHaveAttribute(
      'href',
      '/investments',
    );
  });

  it('warns when the balance is stale', async () => {
    await renderWith(webullView({ isStale: true }));

    expect(
      screen.getByText(`Not updated since ${localDateTime('2026-10-22T18:04:11Z')}`),
    ).toHaveClass('text-warning-dark');
    expect(screen.queryByText(/^As of/)).not.toBeInTheDocument();
    expect(screen.getByText('$12,345.67')).toBeInTheDocument();
  });

  it('keeps the last balance with a muted note when Webull is unreachable', async () => {
    await renderWith(webullView({ lastError: 'Unreachable' }));

    expect(screen.getByText('$12,345.67')).toBeInTheDocument();
    expect(screen.getByText(UNREACHABLE_NOTE)).toHaveClass('text-ink-muted');
    expect(screen.queryByText(UNAVAILABLE)).not.toBeInTheDocument();
  });

  it('says the balance is unavailable, never $0, when Webull is unreachable with nothing fetched', async () => {
    await renderWith(webullView({ lastError: 'Unreachable', balances: [], asOf: null }));

    expect(screen.getByText(UNAVAILABLE)).toBeInTheDocument();
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
    expect(screen.queryByText(UNREACHABLE_NOTE)).not.toBeInTheDocument();
    expect(screen.queryByText(/As of/)).not.toBeInTheDocument();
  });

  it('keeps the balance and links to Settings when Webull rejects the app key', async () => {
    await renderWith(webullView({ lastError: 'KeyRejected' }));

    expect(screen.getByText('$12,345.67')).toBeInTheDocument();
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Webull rejected the app key or secret');
    expect(within(alert).getByRole('link')).toHaveAttribute('href', '/settings');
  });

  it.each<WebullState>(['NotConnected', 'WaitingForApproval', 'ApprovalExpired', 'NeedsVerification'])(
    'keeps the last balance and asks to verify when %s',
    async (state) => {
      const { container } = await renderWith(webullView({ state }));

      expect(screen.getByText('$12,345.67')).toBeInTheDocument();
      const alert = screen.getByRole('alert');
      expect(alert).toHaveTextContent(VERIFY_MESSAGE);
      expect(within(alert).getByRole('link', { name: 'Verify in Settings' })).toHaveAttribute(
        'href',
        '/settings',
      );
      expect(container.querySelector(ANY_INPUT)).toBeNull();
    },
  );

  it('asks to verify without inventing a balance when there is none yet', async () => {
    await renderWith(webullView({ state: 'NotConnected', balances: [], asOf: null }));

    expect(screen.getByRole('alert')).toHaveTextContent(VERIFY_MESSAGE);
    expect(screen.getByText(UNAVAILABLE)).toBeInTheDocument();
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
  });
});
