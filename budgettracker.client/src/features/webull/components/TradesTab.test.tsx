import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { webullService } from '../../../shared/services/webull.service';
import type {
  WebullErrorKind,
  WebullTradeHistoryStatus,
  WebullTradesView,
} from '../../../shared/types/api';
import { localDateTime } from '../../../shared/utils/format';
import { deferred } from '../../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import {
  CLOSED_TRADES,
  EMPTY_TRADE_SUMMARY,
  TRADE_SUMMARY,
  tradeStatus,
  tradesView,
} from '../../../test/webullFixtures';
import TradesTab from './TradesTab';

vi.mock('../../../shared/services/webull.service', () => ({
  webullService: { getTrades: vi.fn(), syncTrades: vi.fn() },
}));

const getTrades = vi.mocked(webullService.getTrades);
const syncTrades = vi.mocked(webullService.syncTrades);
const never = () => new Promise<never>(() => {});

/** Anything that could take a key, secret or token, including `<input type="password">`. */
const ANY_INPUT = 'input, textarea, select, [contenteditable="true"]';
const RANGES = ['1M', '3M', 'YTD', '1Y', 'All'] as const;
const NOT_TAX = 'Before fees · average-cost method · not tax figures';

const renderTab = (queryClient = createTestQueryClient(), strict = false) => {
  const Wrapper = createQueryWrapper(queryClient);
  return render(
    <MemoryRouter>
      <Wrapper>
        <TradesTab />
      </Wrapper>
    </MemoryRouter>,
    { reactStrictMode: strict },
  );
};

/** Renders the tab once the trades GET has resolved to `view`. Sync stays in flight. */
const renderWith = async (view: WebullTradesView) => {
  getTrades.mockResolvedValue(view);
  const result = renderTab();
  await screen.findByLabelText('Trade summary');
  return result;
};

const summary = () => screen.getByLabelText('Trade summary');
const stat = (label: string) => within(summary()).getByText(label).closest('div')!;
const tradesTable = () => screen.getByRole('table', { name: 'Closed trades' });
const rowFor = (symbol: string) =>
  within(tradesTable())
    .getAllByRole('row')
    .find((row) => within(row).queryByText(symbol))!;

describe('TradesTab', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    // The background sync stays in flight unless a test says otherwise.
    syncTrades.mockImplementation(never);
  });

  it('shows a loading placeholder, not zeros, before the trades arrive', () => {
    getTrades.mockImplementation(never);

    renderTab();

    expect(screen.getByRole('status')).toHaveTextContent('Loading your trades');
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
  });

  it('says so, without inventing figures, when the trades cannot be read', async () => {
    getTrades.mockRejectedValue(new Error('500'));

    renderTab();

    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't load your trades");
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
  });

  describe('range', () => {
    it('asks for YTD by default', async () => {
      await renderWith(tradesView());

      expect(getTrades).toHaveBeenCalledExactlyOnceWith('YTD');
      expect(screen.getByRole('radio', { name: 'YTD' })).toHaveAttribute('aria-checked', 'true');
    });

    it.each(RANGES.filter((r) => r !== 'YTD'))('asks for %s when it is picked', async (range) => {
      const user = userEvent.setup();
      await renderWith(tradesView());

      await user.click(screen.getByRole('radio', { name: range }));

      await waitFor(() => expect(getTrades).toHaveBeenLastCalledWith(range));
      expect(screen.getByRole('radio', { name: range })).toHaveAttribute('aria-checked', 'true');
    });

    it('offers exactly the five ranges, in order', async () => {
      await renderWith(tradesView());

      const group = screen.getByRole('radiogroup', { name: 'Trades date range' });
      expect(within(group).getAllByRole('radio').map((r) => r.textContent)).toEqual([...RANGES]);
    });

    it("shows the skeleton, not the last range's figures, while the new range loads", async () => {
      const user = userEvent.setup();
      getTrades.mockImplementation((range) =>
        range === 'YTD' ? Promise.resolve(tradesView()) : never(),
      );
      renderTab();
      await screen.findByLabelText('Trade summary');

      await user.click(screen.getByRole('radio', { name: '1M' }));

      expect(await screen.findByRole('status')).toHaveTextContent('Loading your trades');
      expect(screen.queryByLabelText('Trade summary')).not.toBeInTheDocument();
      expect(screen.queryByText('+$297.25')).not.toBeInTheDocument();
      expect(screen.getByRole('radio', { name: '1M' })).toHaveAttribute('aria-checked', 'true');
    });

    it('states the period the server resolved, as calendar dates', async () => {
      await renderWith(tradesView());
      expect(screen.getByText('Jan 1, 2026 – Oct 24, 2026')).toBeInTheDocument();
    });

    it('says "Through" when the range has no start', async () => {
      await renderWith(tradesView({ from: null }));
      expect(screen.getByText('Through Oct 24, 2026')).toBeInTheDocument();
    });
  });

  it("shows the server's summary figures, with signs and Gain/Loss in words", async () => {
    await renderWith(tradesView());

    // 297.25 is the server's total; the rows sum to 297.50, so no client maths slipped in.
    expect(within(stat('Realized gain/loss')).getByText('+$297.25')).toBeInTheDocument();
    expect(within(stat('Realized gain/loss')).getByText('Gain')).toBeInTheDocument();
    expect(within(stat('Wins')).getByText('2')).toBeInTheDocument();
    expect(within(stat('Losses')).getByText('1')).toBeInTheDocument();
    expect(within(stat('Win rate')).getByText('66.67%')).toBeInTheDocument();
    expect(within(stat('Largest win')).getByText('+$500.00')).toBeInTheDocument();
    expect(within(stat('Largest win')).getByText('Gain')).toBeInTheDocument();
    expect(within(stat('Largest loss')).getByText('−$302.50')).toBeInTheDocument();
    expect(within(stat('Largest loss')).getByText('Loss')).toBeInTheDocument();
    expect(stat('Largest loss').querySelector('[data-trend="down"]')).toHaveTextContent('Loss');
  });

  it(`shows $0.00, not "—", when the range's trades net to zero`, async () => {
    await renderWith(tradesView({ summary: { ...TRADE_SUMMARY, totalRealizedProfitLoss: 0 } }));

    const total = stat('Realized gain/loss');
    expect(within(total).getByText('$0.00')).toBeInTheDocument();
    expect(total).not.toHaveTextContent('—');
    expect(within(total).queryByText(/^(Gain|Loss)$/)).not.toBeInTheDocument();
  });

  it('shows "—" for a win rate the server could not work out', async () => {
    await renderWith(tradesView({ summary: { ...TRADE_SUMMARY, winRate: null } }));

    expect(within(stat('Win rate')).getByText('—')).toBeInTheDocument();
    expect(stat('Win rate')).not.toHaveTextContent('0.00%');
  });

  it('renders one row per closed trade in server order, dates unshifted', async () => {
    await renderWith(tradesView());

    const rows = within(tradesTable()).getAllByRole('row').slice(1);
    const cells = (i: number) => rows.map((row) => within(row).getAllByRole('cell')[i].textContent);
    // Jan 2 read through UTC would show Jan 1 in New York (the test zone).
    expect(cells(0)).toEqual(['Oct 1, 2026', 'Jun 15, 2026', 'Jan 2, 2026']);
    expect(cells(1)).toEqual(['TSLAStock', 'BTCUSDCrypto', 'AAPLStock']);
  });

  it('shows quantity, prices and realized $ and % from the server', async () => {
    await renderWith(tradesView());

    const tsla = within(rowFor('TSLA'));
    expect(tsla.getByText('10')).toBeInTheDocument();
    expect(tsla.getByText('$250.50')).toBeInTheDocument();
    expect(tsla.getByText('$220.25')).toBeInTheDocument();
    expect(tsla.getByText('−$302.50')).toBeInTheDocument();
    expect(tsla.getByText('−12.08%')).toBeInTheDocument();
    expect(rowFor('TSLA').querySelector('[data-trend="down"]')).toHaveTextContent('Loss');

    const btc = within(rowFor('BTCUSD'));
    expect(btc.getByText('0.01')).toBeInTheDocument();
    expect(btc.getByText('$60,000.00')).toBeInTheDocument();
    expect(btc.getByText('$70,000.00')).toBeInTheDocument();
    expect(btc.getByText('+$100.00')).toBeInTheDocument();
    expect(btc.getByText('+16.67%')).toBeInTheDocument();
    expect(rowFor('BTCUSD').querySelector('[data-trend="up"]')).toHaveTextContent('Gain');
  });

  it("shows a price of $1 or more in cents and keeps a sub-dollar price's digits", async () => {
    const [tsla] = CLOSED_TRADES;
    await renderWith(tradesView({ trades: [{ ...tsla, averageCost: 531.378333, salePrice: 0.5 }] }));

    const row = within(rowFor('TSLA'));
    expect(row.getByText('$531.38')).toBeInTheDocument();
    expect(row.getByText('$0.50')).toBeInTheDocument();
    expect(rowFor('TSLA')).not.toHaveTextContent('531.378333');
  });

  it('shows "—" for a realized % the server did not supply, never 0.00%', async () => {
    await renderWith(tradesView());

    const aapl = rowFor('AAPL');
    expect(within(aapl).getByText('+$500.00')).toBeInTheDocument();
    expect(within(aapl).getByText('—')).toBeInTheDocument();
    expect(aapl).not.toHaveTextContent('0.00%');
    expect(within(aapl).getByText('Gain')).toBeInTheDocument();
  });

  it('shows the empty copy, no table and no $0 for a range with no trades', async () => {
    await renderWith(tradesView({ trades: [], summary: EMPTY_TRADE_SUMMARY }));

    expect(screen.getByText('No closed trades in this range')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(document.body).not.toHaveTextContent('$0');
    expect(within(stat('Realized gain/loss')).getByText('—')).toBeInTheDocument();
    expect(within(stat('Wins')).getByText('0')).toBeInTheDocument();
    expect(within(stat('Losses')).getByText('0')).toBeInTheDocument();
    expect(within(summary()).queryByText(/^(Gain|Loss)$/)).not.toBeInTheDocument();
  });

  describe('excluded trades', () => {
    it('names both exclusions, plural', async () => {
      await renderWith(
        tradesView({
          summary: { ...TRADE_SUMMARY, excludedIncompleteCost: 3, excludedUnsupported: 2 },
        }),
      );

      expect(screen.getByText('3 trades excluded — cost history incomplete')).toBeInTheDocument();
      expect(screen.getByText('2 option/short orders not included')).toBeInTheDocument();
    });

    it('names both exclusions, singular', async () => {
      await renderWith(
        tradesView({
          summary: { ...TRADE_SUMMARY, excludedIncompleteCost: 1, excludedUnsupported: 1 },
        }),
      );

      expect(screen.getByText('1 trade excluded — cost history incomplete')).toBeInTheDocument();
      expect(screen.getByText('1 option/short order not included')).toBeInTheDocument();
    });

    it('says nothing when nothing was excluded', async () => {
      await renderWith(tradesView());

      expect(screen.queryByText(/excluded/)).not.toBeInTheDocument();
      expect(screen.queryByText(/not included/)).not.toBeInTheDocument();
    });
  });

  it.each<[string, Partial<WebullTradesView>]>([
    ['with trades', {}],
    ['for an empty range', { trades: [], summary: EMPTY_TRADE_SUMMARY }],
    ['after a failed sync', { history: tradeStatus({ lastError: 'Unreachable' }) }],
    ['while history loads', { history: tradeStatus({ isComplete: false }) }],
  ])('labels the figures as before fees and not tax figures %s', async (_, overrides) => {
    await renderWith(tradesView(overrides));
    expect(screen.getByText(NOT_TAX)).toBeInTheDocument();
  });

  describe('history notes', () => {
    const LOADING = /Loading trade history/;
    const PROVISIONAL = /may change/;

    /** Renders with the opening sync resolved to `status`, so the chain has stopped. */
    const renderStopped = async (status: WebullTradeHistoryStatus) => {
      syncTrades.mockResolvedValueOnce(status);
      getTrades.mockResolvedValue(tradesView({ history: status }));
      renderTab();
      await screen.findByLabelText('Trade summary');
      await waitFor(() => expect(syncTrades).toHaveBeenCalledTimes(1));
    };

    it('says how far back it is loading while the sync runs', async () => {
      await renderWith(tradesView({ history: tradeStatus({ isComplete: false }) }));

      expect(screen.getByText('Loading trade history back to Oct 24, 2024')).toBeInTheDocument();
      expect(screen.queryByText(PROVISIONAL)).not.toBeInTheDocument();
    });

    it('falls back to a plain loading note when the start is unknown', async () => {
      await renderWith(
        tradesView({ history: tradeStatus({ isComplete: false, historyFrom: null }) }),
      );
      expect(screen.getByText('Loading trade history…')).toBeInTheDocument();
    });

    it('shows neither note once history is complete', async () => {
      await renderStopped(tradeStatus());

      expect(screen.queryByText(LOADING)).not.toBeInTheDocument();
      expect(screen.queryByText(PROVISIONAL)).not.toBeInTheDocument();
    });

    it.each<[string, WebullTradeHistoryStatus]>([
      ['a sync error', tradeStatus({ isComplete: false, lastError: 'Unreachable' })],
      ['Webull not Connected', tradeStatus({ isComplete: false, state: 'NeedsVerification' })],
    ])('flags the figures as provisional once %s stops an incomplete sync', async (_, status) => {
      await renderStopped(status);

      expect(
        await screen.findByText(
          "Trade history loaded back to Oct 24, 2024. Older trades aren't included yet, so average costs and totals may change.",
        ),
      ).toBeInTheDocument();
      expect(screen.queryByText(LOADING)).not.toBeInTheDocument();
    });

    it("doesn't flash the provisional note while the last re-read is in flight", async () => {
      const reread = deferred<WebullTradesView>();
      getTrades
        .mockResolvedValueOnce(tradesView({ history: tradeStatus({ isComplete: false }) }))
        .mockReturnValueOnce(reread.promise);
      const last = deferred<WebullTradeHistoryStatus>();
      syncTrades.mockReturnValueOnce(last.promise);

      renderTab();
      await screen.findByText(LOADING);
      last.resolve(tradeStatus());
      await waitFor(() => expect(getTrades).toHaveBeenCalledTimes(2));
      // The old, incomplete status is still on screen: it must still read as loading.
      expect(screen.getByText(LOADING)).toBeInTheDocument();
      expect(screen.queryByText(PROVISIONAL)).not.toBeInTheDocument();

      reread.resolve(tradesView());
      await waitFor(() => expect(screen.queryByText(LOADING)).not.toBeInTheDocument());
      expect(screen.queryByText(PROVISIONAL)).not.toBeInTheDocument();
    });

    it('flags the figures as provisional when the sync itself fails', async () => {
      syncTrades.mockRejectedValueOnce(new Error('502'));

      await renderWith(tradesView({ history: tradeStatus({ isComplete: false }) }));

      expect(await screen.findByText(PROVISIONAL)).toBeInTheDocument();
      expect(screen.queryByText(LOADING)).not.toBeInTheDocument();
    });

    it('falls back to a plain provisional note when the start is unknown', async () => {
      await renderStopped(
        tradeStatus({ isComplete: false, historyFrom: null, lastError: 'Unreachable' }),
      );

      expect(
        await screen.findByText(
          'Trade history is still incomplete, so average costs and totals may change.',
        ),
      ).toBeInTheDocument();
    });
  });

  it('says when the trades are from, in local time', async () => {
    await renderWith(tradesView());
    expect(screen.getByText(`As of ${localDateTime('2026-10-24T20:05:00Z')}`)).toBeInTheDocument();
  });

  it.each<[WebullErrorKind, string]>([
    ['Unreachable', "Couldn't reach Webull. Showing the last known trade history."],
    ['KeyRejected', 'Webull rejected the app key or secret'],
  ])('keeps the rows when the last sync hit %s', async (lastError, message) => {
    await renderWith(tradesView({ history: tradeStatus({ lastError }) }));

    expect(screen.getByText(message)).toBeInTheDocument();
    expect(within(tradesTable()).getAllByRole('row')).toHaveLength(4);
  });

  it('keeps the rows and links to Settings when Webull needs verification', async () => {
    await renderWith(tradesView({ history: tradeStatus({ state: 'NeedsVerification' }) }));

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Webull needs you to verify in the Webull app');
    expect(within(alert).getByRole('link', { name: 'Verify in Settings' })).toHaveAttribute(
      'href',
      '/settings',
    );
    expect(within(tradesTable()).getAllByRole('row')).toHaveLength(4);
  });

  it('has no inputs: the range buttons are the only controls', async () => {
    const { container } = await renderWith(tradesView());

    expect(container.querySelector(ANY_INPUT)).toBeNull();
    const buttons = [...container.querySelectorAll('button')];
    expect(buttons.map((b) => [b.getAttribute('role'), b.textContent])).toEqual(
      RANGES.map((r) => ['radio', r]),
    );
  });

  describe('sync on open', () => {
    const incomplete = (overrides: Partial<WebullTradeHistoryStatus> = {}) =>
      tradeStatus({ isComplete: false, ...overrides });

    beforeEach(() => {
      getTrades.mockResolvedValue(tradesView());
    });

    it('syncs once on open, even under StrictMode, then re-reads the trades', async () => {
      syncTrades.mockResolvedValueOnce(tradeStatus());
      const queryClient = createTestQueryClient();
      const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

      renderTab(queryClient, true);

      await waitFor(() => expect(getTrades).toHaveBeenCalledTimes(2));
      expect(syncTrades).toHaveBeenCalledTimes(1);
      expect(invalidate).toHaveBeenCalledTimes(1);
    });

    it.each<[string, WebullTradeHistoryStatus]>([
      ['a stored lastError', incomplete({ lastError: 'Unreachable' })],
      ['a stored not-Connected state', incomplete({ state: 'NeedsVerification' })],
    ])('opening Trades with %s still syncs once', async (_, stored) => {
      getTrades.mockResolvedValue(tradesView({ history: stored }));

      renderTab();

      await screen.findByLabelText('Trade summary');
      await waitFor(() => expect(syncTrades).toHaveBeenCalledTimes(1));
    });

    it('starts the next sync only after the last one resolves, while incomplete', async () => {
      const first = deferred<WebullTradeHistoryStatus>();
      syncTrades.mockReturnValueOnce(first.promise).mockImplementation(never);

      renderTab();
      await screen.findByLabelText('Trade summary');
      expect(syncTrades).toHaveBeenCalledTimes(1);

      first.resolve(incomplete());
      await waitFor(() => expect(syncTrades).toHaveBeenCalledTimes(2));
    });

    it('stops once history is complete', async () => {
      syncTrades.mockResolvedValueOnce(incomplete()).mockResolvedValueOnce(tradeStatus());

      renderTab();

      await waitFor(() => expect(getTrades).toHaveBeenCalledTimes(3));
      expect(syncTrades).toHaveBeenCalledTimes(2);
    });

    it.each<[string, WebullTradeHistoryStatus]>([
      ['a sync error', incomplete({ lastError: 'Unreachable' })],
      ['a rejected key', incomplete({ lastError: 'KeyRejected' })],
      ['Webull not Connected', incomplete({ state: 'NeedsVerification' })],
    ])('stops on %s', async (_, status) => {
      // Once: a chain that wrongly carried on would hit the never-settling default and stall.
      syncTrades.mockResolvedValueOnce(status);

      renderTab();

      await waitFor(() => expect(getTrades).toHaveBeenCalledTimes(2));
      expect(syncTrades).toHaveBeenCalledTimes(1);
    });

    it('stops on unmount, without re-reading', async () => {
      const first = deferred<WebullTradeHistoryStatus>();
      syncTrades.mockReturnValueOnce(first.promise).mockResolvedValueOnce(incomplete());
      const queryClient = createTestQueryClient();
      const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

      const { unmount } = renderTab(queryClient);
      await waitFor(() => expect(syncTrades).toHaveBeenCalledTimes(1));
      unmount();
      first.resolve(incomplete());
      await first.promise;
      await new Promise((resolve) => setTimeout(resolve, 0));

      expect(syncTrades).toHaveBeenCalledTimes(1);
      expect(invalidate).not.toHaveBeenCalled();
    });

    it('joins a sync still in flight when reopened, instead of overlapping it', async () => {
      const first = deferred<WebullTradeHistoryStatus>();
      syncTrades.mockReturnValueOnce(first.promise).mockImplementation(never);
      const queryClient = createTestQueryClient();

      renderTab(queryClient).unmount();
      renderTab(queryClient);
      await screen.findByLabelText('Trade summary');
      expect(syncTrades).toHaveBeenCalledTimes(1);

      first.resolve(incomplete());
      await waitFor(() => expect(syncTrades).toHaveBeenCalledTimes(2));
    });

    it('invalidates only the trades queries after each sync', async () => {
      syncTrades.mockResolvedValueOnce(incomplete()).mockResolvedValueOnce(tradeStatus());
      const queryClient = createTestQueryClient();
      const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

      renderTab(queryClient);

      await waitFor(() => expect(syncTrades).toHaveBeenCalledTimes(2));
      await waitFor(() => expect(invalidate).toHaveBeenCalledTimes(2));
      expect(invalidate.mock.calls.map(([filters]) => filters)).toEqual([
        { queryKey: ['webull-trades'] },
        { queryKey: ['webull-trades'] },
      ]);
    });

    it('never retries a failed sync, and keeps the trades up', async () => {
      syncTrades.mockRejectedValueOnce(new Error('502'));

      renderTab();

      expect(await screen.findByLabelText('Trade summary')).toBeInTheDocument();
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(syncTrades).toHaveBeenCalledTimes(1);
      expect(getTrades).toHaveBeenCalledTimes(1);
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
  });
});

