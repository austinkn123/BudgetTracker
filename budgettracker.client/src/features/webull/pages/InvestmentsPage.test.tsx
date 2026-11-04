import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { webullService } from '../../../shared/services/webull.service';
import type {
  WebullErrorKind,
  WebullHoldingsView,
  WebullTradeHistoryStatus,
} from '../../../shared/types/api';
import { localDateTime } from '../../../shared/utils/format';
import { deferred } from '../../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import {
  HOLDINGS_ACCOUNT,
  holdingsView,
  tradeStatus,
  tradesView,
  webullView,
} from '../../../test/webullFixtures';
import InvestmentsPage from './InvestmentsPage';

vi.mock('../../../shared/services/webull.service', () => ({
  webullService: {
    get: vi.fn(),
    refresh: vi.fn(),
    verify: vi.fn(),
    getHoldings: vi.fn(),
    getTrades: vi.fn(),
    syncTrades: vi.fn(),
  },
}));

const getHoldings = vi.mocked(webullService.getHoldings);
const refresh = vi.mocked(webullService.refresh);
const getTrades = vi.mocked(webullService.getTrades);
const syncTrades = vi.mocked(webullService.syncTrades);
const never = () => new Promise<never>(() => {});

/** Anything that could take a key, secret or token, including `<input type="password">`. */
const ANY_INPUT = 'input, textarea, select, [contenteditable="true"]';
const AS_OF = '2026-10-23T20:31:04Z';

/** The query string, so a test can see which tab the URL holds. */
const Search = () => <p data-testid="search">{useLocation().search}</p>;

const renderPage = (
  queryClient = createTestQueryClient(),
  strict = false,
  entry = '/investments',
) => {
  const Wrapper = createQueryWrapper(queryClient);
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <Wrapper>
        <Routes>
          <Route path="/" element={<p>Dashboard page</p>} />
          <Route path="/investments" element={<InvestmentsPage />} />
        </Routes>
        <Search />
      </Wrapper>
    </MemoryRouter>,
    { reactStrictMode: strict },
  );
};

/** Renders the page once the holdings GET has resolved to `view`. */
const renderWith = async (view: WebullHoldingsView) => {
  getHoldings.mockResolvedValue(view);
  const result = renderPage();
  await screen.findByRole('heading', { name: 'Investments' });
  return result;
};

const holdingsTable = () => screen.getByRole('table', { name: 'Individual Cash holdings' });
const rowFor = (symbol: string) =>
  within(holdingsTable())
    .getAllByRole('row')
    .find((row) => within(row).queryByText(symbol))!;

describe('InvestmentsPage', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    // The background refresh and trade sync stay in flight unless a test says otherwise.
    refresh.mockImplementation(never);
    syncTrades.mockImplementation(never);
    getTrades.mockResolvedValue(tradesView());
  });

  it('shows a loading placeholder, not zeros, before the holdings arrive', () => {
    getHoldings.mockImplementation(never);

    renderPage();

    expect(screen.getByRole('status')).toHaveTextContent('Loading your holdings');
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
  });

  it('says so, without inventing figures, when the holdings cannot be read', async () => {
    getHoldings.mockRejectedValue(new Error('500'));

    renderPage();

    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't load your holdings");
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
  });

  it('redirects to the Dashboard when Webull is not set up', async () => {
    getHoldings.mockResolvedValue(holdingsView({ state: 'NotSetUp', accounts: [], asOf: null }));

    renderPage();

    expect(await screen.findByText('Dashboard page')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Investments' })).not.toBeInTheDocument();
  });

  it("shows the account's figures in its own currency, with gain and loss in words", async () => {
    await renderWith(holdingsView());

    const header = screen.getByLabelText('Individual Cash summary');
    expect(within(header).getByText('$12,500.00')).toBeInTheDocument();
    expect(within(header).getByText('$1,500.00')).toBeInTheDocument();
    expect(within(header).getByText('−$42.10')).toBeInTheDocument();
    expect(within(header).getByText('+$2,995.00')).toBeInTheDocument();
    expect(within(header).getByText('Loss')).toBeInTheDocument();
    expect(within(header).getByText('Gain')).toBeInTheDocument();
  });

  it('renders one row per position in server order, with 2 dp percents and gains in words', async () => {
    const { container } = await renderWith(holdingsView());

    const rows = within(holdingsTable()).getAllByRole('row').slice(1);
    expect(rows.map((row) => within(row).getAllByRole('cell')[0].textContent)).toEqual([
      'BTCUSDCrypto',
      'AAPLStock',
      'XYZEVENT',
    ]);

    const btc = within(rowFor('BTCUSD'));
    expect(btc.getByText('0.05')).toBeInTheDocument();
    expect(btc.getByText('$100,000.00')).toBeInTheDocument();
    expect(btc.getByText('$5,000.00')).toBeInTheDocument();
    expect(btc.getByText('$6,500.00')).toBeInTheDocument();
    expect(btc.getByText('+$1,500.00')).toBeInTheDocument();
    expect(btc.getByText('+30.00%')).toBeInTheDocument();
    expect(btc.getByText('59.09%')).toBeInTheDocument();
    expect(btc.getByText('Gain')).toBeInTheDocument();

    const aapl = within(rowFor('AAPL'));
    expect(aapl.getByText('20')).toBeInTheDocument();
    expect(aapl.getByText('$150.25')).toBeInTheDocument();
    expect(aapl.getByText('$3,005.00')).toBeInTheDocument();
    expect(aapl.getByText('$4,500.00')).toBeInTheDocument();
    expect(aapl.getByText('+$1,495.00')).toBeInTheDocument();
    expect(aapl.getByText('+49.75%')).toBeInTheDocument();
    expect(aapl.getByText('40.91%')).toBeInTheDocument();

    expect(container.querySelector(ANY_INPUT)).toBeNull();
  });

  it("shows a price of $1 or more in cents and keeps a sub-dollar price's digits", async () => {
    const [btc, aapl] = HOLDINGS_ACCOUNT.positions;
    await renderWith(
      holdingsView({
        accounts: [
          {
            ...HOLDINGS_ACCOUNT,
            positions: [{ ...aapl, averageCost: 531.378333 }, { ...btc, averageCost: 0.000012 }],
          },
        ],
      }),
    );

    expect(within(rowFor('AAPL')).getByText('$531.38')).toBeInTheDocument();
    expect(within(rowFor('BTCUSD')).getByText('$0.000012')).toBeInTheDocument();
  });

  it('shows "—" for every figure Webull did not supply, never $0', async () => {
    await renderWith(holdingsView());

    const event = rowFor('XYZ');
    // Quantity is known (1); the six computed figures are not.
    expect(within(event).getAllByText('—')).toHaveLength(6);
    expect(event).not.toHaveTextContent('$0');
    expect(event).not.toHaveTextContent('0.00%');
    expect(within(event).queryByText(/Gain|Loss/)).not.toBeInTheDocument();
  });

  it('shows the open return % beside the open gain/loss', async () => {
    await renderWith(holdingsView());

    const openStat = within(screen.getByLabelText('Individual Cash summary'))
      .getByText('Open gain/loss')
      .closest('div')!;
    expect(within(openStat).getByText('+$2,995.00')).toBeInTheDocument();
    expect(within(openStat).getByText('+37.41%')).toBeInTheDocument();
    expect(within(openStat).getByText('Gain')).toBeInTheDocument();
  });

  it('shows "—" for an open return % Webull did not report', async () => {
    await renderWith(
      holdingsView({ accounts: [{ ...HOLDINGS_ACCOUNT, openProfitLossPercent: null }] }),
    );

    const openStat = within(screen.getByLabelText('Individual Cash summary'))
      .getByText('Open gain/loss')
      .closest('div')!;
    expect(within(openStat).getByText('+$2,995.00')).toBeInTheDocument();
    expect(within(openStat).getByText('—')).toBeInTheDocument();
    expect(openStat).not.toHaveTextContent('0.00%');
  });

  it('labels allocation from percentOfHoldings, with cash shown separately', async () => {
    await renderWith(holdingsView());

    const allocation = screen.getByRole('list', { name: 'Individual Cash allocation' });
    expect(within(allocation).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'BTCUSD59.09%',
      'AAPL40.91%',
      'XYZ—',
    ]);
    expect(
      screen.getByText("Cash of $1,500.00 isn't counted in these percentages."),
    ).toBeInTheDocument();
  });

  it('shows no allocation for an account with no positions', async () => {
    await renderWith(holdingsView({ accounts: [{ ...HOLDINGS_ACCOUNT, positions: [] }] }));

    expect(screen.queryByRole('list', { name: /allocation/ })).not.toBeInTheDocument();
  });

  describe('day gain/loss when the market is closed', () => {
    const dayStat = () =>
      within(screen.getByLabelText('Individual Cash summary'))
        .getByText('Day gain/loss')
        .closest('div')!;

    it('says the market was closed when a weekend pull reports $0.00', async () => {
      // Sunday morning in New York.
      await renderWith(
        holdingsView({
          asOf: '2026-10-25T14:00:00Z',
          accounts: [{ ...HOLDINGS_ACCOUNT, dayProfitLoss: 0 }],
        }),
      );

      expect(within(dayStat()).getByText('$0.00')).toBeInTheDocument();
      expect(within(dayStat()).getByText('Market closed')).toBeInTheDocument();
    });

    it('shows the plain amount on a weekday, judged in New York time, not UTC', async () => {
      // Saturday 03:00 UTC is still Friday 11 PM in New York: the market had a day.
      await renderWith(
        holdingsView({
          asOf: '2026-10-24T03:00:00Z',
          accounts: [{ ...HOLDINGS_ACCOUNT, dayProfitLoss: 0 }],
        }),
      );

      expect(within(dayStat()).getByText('$0.00')).toBeInTheDocument();
      expect(screen.queryByText('Market closed')).not.toBeInTheDocument();
    });

    it('keeps a real weekend move (crypto trades every day) as a gain or loss', async () => {
      await renderWith(
        holdingsView({
          asOf: '2026-10-25T14:00:00Z',
          accounts: [{ ...HOLDINGS_ACCOUNT, dayProfitLoss: -12.5 }],
        }),
      );

      expect(within(dayStat()).getByText('−$12.50')).toBeInTheDocument();
      expect(within(dayStat()).getByText('Loss')).toBeInTheDocument();
      expect(screen.queryByText('Market closed')).not.toBeInTheDocument();
    });
  });

  it('marks direction with an up or down arrow, keeping the sign and the word', async () => {
    const [btc, aapl] = HOLDINGS_ACCOUNT.positions;
    await renderWith(
      holdingsView({
        accounts: [
          {
            ...HOLDINGS_ACCOUNT,
            positions: [btc, { ...aapl, openProfitLoss: -250.5, openProfitLossPercent: -3.1 }],
          },
        ],
      }),
    );

    const up = rowFor('BTCUSD').querySelector('[data-trend="up"]');
    expect(up).toHaveTextContent('Gain');
    expect(rowFor('BTCUSD')).toHaveTextContent('+$1,500.00');

    const down = rowFor('AAPL').querySelector('[data-trend="down"]');
    expect(down).toHaveTextContent('Loss');
    expect(rowFor('AAPL')).toHaveTextContent('−$250.50');

    const summary = screen.getByLabelText('Individual Cash summary');
    expect(summary.querySelector('[data-trend="down"]')).toHaveTextContent('Loss');
    expect(summary.querySelector('[data-trend="up"]')).toHaveTextContent('Gain');
  });

  it('labels a loss in words, with a minus sign', async () => {
    const [btc] = HOLDINGS_ACCOUNT.positions;
    await renderWith(
      holdingsView({
        accounts: [
          {
            ...HOLDINGS_ACCOUNT,
            positions: [{ ...btc, openProfitLoss: -250.5, openProfitLossPercent: -3.1 }],
          },
        ],
      }),
    );

    const row = within(rowFor('BTCUSD'));
    expect(row.getByText('−$250.50')).toBeInTheDocument();
    expect(row.getByText('−3.10%')).toBeInTheDocument();
    expect(row.getByText('Loss')).toBeInTheDocument();
  });

  it('renders two positions that share a symbol', async () => {
    // Nothing server-side makes a symbol unique within an account (e.g. two lots of one stock).
    const errors = vi.spyOn(console, 'error').mockImplementation(() => {});
    const [, aapl] = HOLDINGS_ACCOUNT.positions;
    await renderWith(
      holdingsView({
        accounts: [
          { ...HOLDINGS_ACCOUNT, positions: [aapl, { ...aapl, quantity: 5, marketValue: 1125 }] },
        ],
      }),
    );

    const rows = within(holdingsTable()).getAllByRole('row').slice(1);
    expect(rows).toHaveLength(2);
    expect(within(rows[0]).getByText('$4,500.00')).toBeInTheDocument();
    expect(within(rows[1]).getByText('5')).toBeInTheDocument();
    expect(within(rows[1]).getByText('$1,125.00')).toBeInTheDocument();
    expect(errors.mock.calls.flat().join(' ')).not.toMatch(/same key/i);
  });

  it('shows "—" for an unknown quantity', async () => {
    const [, aapl] = HOLDINGS_ACCOUNT.positions;
    await renderWith(
      holdingsView({ accounts: [{ ...HOLDINGS_ACCOUNT, positions: [{ ...aapl, quantity: null }] }] }),
    );

    const row = within(rowFor('AAPL'));
    expect(row.getAllByText('—')).toHaveLength(1);
    expect(row.queryByText('20')).not.toBeInTheDocument();
    expect(row.queryByText('0')).not.toBeInTheDocument();
  });

  it('shows a reported zero gain as $0.00 with no sign or Gain/Loss word', async () => {
    const [, aapl] = HOLDINGS_ACCOUNT.positions;
    await renderWith(
      holdingsView({
        accounts: [
          {
            ...HOLDINGS_ACCOUNT,
            positions: [{ ...aapl, openProfitLoss: 0, openProfitLossPercent: 0 }],
          },
        ],
      }),
    );

    const row = rowFor('AAPL');
    expect(within(row).getByText('$0.00')).toBeInTheDocument();
    expect(within(row).getByText('0.00%')).toBeInTheDocument();
    expect(row).not.toHaveTextContent(/[+−]\$0\.00|[+−]0\.00%/);
    expect(within(row).queryByText(/^(Gain|Loss)$/)).not.toBeInTheDocument();
  });

  it('shows "—" in the account summary for cash, day and open gain/loss Webull did not report', async () => {
    await renderWith(
      holdingsView({
        accounts: [
          { ...HOLDINGS_ACCOUNT, cash: null, dayProfitLoss: null, openProfitLoss: null },
        ],
      }),
    );

    const summary = screen.getByLabelText('Individual Cash summary');
    expect(within(summary).getAllByText('—')).toHaveLength(3);
    expect(within(summary).queryByText(/^(Gain|Loss)$/)).not.toBeInTheDocument();
    expect(summary).not.toHaveTextContent('$0');
    expect(within(summary).getByText('$12,500.00')).toBeInTheDocument();
  });

  it('shows a non-dollar account in its own currency', async () => {
    await renderWith(holdingsView({ accounts: [{ ...HOLDINGS_ACCOUNT, currency: 'CAD' }] }));

    const summary = within(screen.getByLabelText('Individual Cash summary'));
    expect(summary.getByText('CA$12,500.00')).toBeInTheDocument();
    expect(summary.getByText('CA$1,500.00')).toBeInTheDocument();

    const btc = within(rowFor('BTCUSD'));
    expect(btc.getByText('CA$100,000.00')).toBeInTheDocument();
    expect(btc.getByText('CA$6,500.00')).toBeInTheDocument();
    expect(btc.getByText('+CA$1,500.00')).toBeInTheDocument();
  });

  it('renders each account as its own section with its own table', async () => {
    await renderWith(
      holdingsView({
        accounts: [
          HOLDINGS_ACCOUNT,
          { ...HOLDINGS_ACCOUNT, accountId: 'ACC2', accountLabel: 'Roth IRA', positions: [] },
        ],
      }),
    );

    const individual = within(screen.getByRole('region', { name: 'Individual Cash' }));
    expect(individual.getAllByRole('row')).toHaveLength(4);
    expect(individual.queryByText('No positions')).not.toBeInTheDocument();

    const roth = within(screen.getByRole('region', { name: 'Roth IRA' }));
    expect(roth.getByText('No positions')).toBeInTheDocument();
    expect(roth.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getAllByText('No positions')).toHaveLength(1);
    expect(screen.getAllByRole('table')).toHaveLength(1);
  });

  it('Connected with nothing fetched shows the unavailable note, no table, no $', async () => {
    await renderWith(holdingsView({ accounts: [], asOf: null }));

    expect(screen.getByText('Webull balance unavailable right now')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.queryByText(/\$/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Prices from Webull/)).not.toBeInTheDocument();
  });

  it('shows "No positions" and the cash for an empty account', async () => {
    await renderWith(holdingsView({ accounts: [{ ...HOLDINGS_ACCOUNT, positions: [] }] }));

    expect(screen.getByText('No positions')).toBeInTheDocument();
    expect(screen.getByText('$1,500.00')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('says where the prices come from and when, and never calls them live', async () => {
    await renderWith(holdingsView());

    expect(
      screen.getByText(`Prices from Webull as of ${localDateTime(AS_OF)}`),
    ).toBeInTheDocument();
    expect(document.body).not.toHaveTextContent(/\blive\b/i);
  });

  it('warns when the prices are stale', async () => {
    await renderWith(holdingsView({ isStale: true }));

    expect(screen.getByText(`Not updated since ${localDateTime(AS_OF)}`)).toHaveClass(
      'text-warning-dark',
    );
    expect(screen.getByText('$6,500.00')).toBeInTheDocument();
  });

  it.each<[WebullErrorKind, string]>([
    ['Unreachable', "Couldn't reach Webull. Showing the last known balance."],
    ['KeyRejected', 'Webull rejected the app key or secret'],
  ])('keeps the rows when the last refresh hit %s', async (lastError, message) => {
    await renderWith(holdingsView({ lastError }));

    expect(screen.getByText(message)).toBeInTheDocument();
    expect(within(holdingsTable()).getAllByRole('row')).toHaveLength(4);
  });

  it('keeps the "As of" time when the last refresh failed', async () => {
    await renderWith(holdingsView({ lastError: 'Unreachable' }));

    expect(
      screen.getByText(`Prices from Webull as of ${localDateTime(AS_OF)}`),
    ).toBeInTheDocument();
    expect(within(holdingsTable()).getAllByRole('row')).toHaveLength(4);
  });

  it('keeps the rows and links to Settings when Webull needs verification', async () => {
    await renderWith(holdingsView({ state: 'NeedsVerification' }));

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent(
      'Webull needs you to verify in the Webull app before the balance can update',
    );
    expect(within(alert).getByRole('link', { name: 'Verify in Settings' })).toHaveAttribute(
      'href',
      '/settings',
    );
    expect(within(holdingsTable()).getAllByRole('row')).toHaveLength(4);
  });

  it('refreshes once per mount, then re-reads the holdings', async () => {
    getHoldings.mockResolvedValue(holdingsView());
    refresh.mockResolvedValue(webullView());
    const queryClient = createTestQueryClient();
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

    renderPage(queryClient, true);

    await waitFor(() => expect(getHoldings).toHaveBeenCalledTimes(2));
    expect(refresh).toHaveBeenCalledTimes(1);
    expect(invalidate.mock.calls.map(([filters]) => filters?.queryKey)).toEqual([
      ['webull-holdings'],
    ]);
    expect(await screen.findByText('$6,500.00')).toBeInTheDocument();
  });

  it('keeps the page up when the refresh fails', async () => {
    const holdings = deferred<WebullHoldingsView>();
    getHoldings.mockReturnValue(holdings.promise);
    refresh.mockRejectedValue(new Error('502'));

    renderPage();
    holdings.resolve(holdingsView());

    expect(await screen.findByText('$6,500.00')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  describe('tabs', () => {
    const tab = (name: string) => screen.getByRole('tab', { name });

    it('opens on Holdings, untouched, without reading or syncing trades', async () => {
      await renderWith(holdingsView());

      expect(within(screen.getByRole('tablist', { name: 'Investments' })).getAllByRole('tab')
        .map((t) => t.textContent)).toEqual(['Holdings', 'Trades']);
      expect(tab('Holdings')).toHaveAttribute('aria-selected', 'true');
      expect(tab('Trades')).toHaveAttribute('aria-selected', 'false');
      expect(screen.getByRole('tabpanel', { name: 'Holdings' })).toContainElement(holdingsTable());
      expect(getTrades).not.toHaveBeenCalled();
      expect(syncTrades).not.toHaveBeenCalled();
      expect(screen.getByTestId('search')).toBeEmptyDOMElement();
    });

    it('switches to Trades and back, keeping the tab in the URL', async () => {
      const user = userEvent.setup();
      await renderWith(holdingsView());

      await user.click(tab('Trades'));

      const panel = await screen.findByRole('tabpanel', { name: 'Trades' });
      expect(await within(panel).findByRole('table', { name: 'Closed trades' })).toBeInTheDocument();
      expect(within(panel).getByText('TSLA')).toBeInTheDocument();
      expect(screen.queryByRole('table', { name: 'Individual Cash holdings' })).not.toBeInTheDocument();
      expect(screen.getByTestId('search')).toHaveTextContent('?tab=trades');
      expect(getTrades).toHaveBeenCalledWith('YTD');

      await user.click(tab('Holdings'));

      expect(holdingsTable()).toBeInTheDocument();
      expect(screen.queryByRole('table', { name: 'Closed trades' })).not.toBeInTheDocument();
      expect(screen.getByTestId('search')).toBeEmptyDOMElement();
    });

    it('opens on Trades from ?tab=trades, so a reload keeps it', async () => {
      getHoldings.mockResolvedValue(holdingsView());

      renderPage(createTestQueryClient(), false, '/investments?tab=trades');

      expect(await screen.findByRole('table', { name: 'Closed trades' })).toBeInTheDocument();
      expect(tab('Trades')).toHaveAttribute('aria-selected', 'true');
      expect(screen.queryByRole('table', { name: 'Individual Cash holdings' })).not.toBeInTheDocument();
    });

    it('moves between tabs with the arrow keys', async () => {
      const user = userEvent.setup();
      await renderWith(holdingsView());

      await user.tab();
      expect(tab('Holdings')).toHaveFocus();
      await user.keyboard('{ArrowRight}');

      expect(tab('Trades')).toHaveFocus();
      expect(tab('Trades')).toHaveAttribute('aria-selected', 'true');
      await user.keyboard('{ArrowRight}');
      expect(tab('Holdings')).toHaveFocus();
    });

    it('has no inputs on Trades: only the tab and range buttons', async () => {
      getHoldings.mockResolvedValue(holdingsView());
      const { container } = renderPage(createTestQueryClient(), false, '/investments?tab=trades');
      await screen.findByRole('table', { name: 'Closed trades' });

      expect(container.querySelector(ANY_INPUT)).toBeNull();
      expect([...container.querySelectorAll('button')].map((b) => b.getAttribute('role'))).toEqual(
        ['tab', 'tab', 'radio', 'radio', 'radio', 'radio', 'radio'],
      );
    });

    it('stops the trade sync when you leave the tab', async () => {
      const user = userEvent.setup();
      const first = deferred<WebullTradeHistoryStatus>();
      syncTrades.mockReturnValueOnce(first.promise).mockResolvedValue(tradeStatus());
      getHoldings.mockResolvedValue(holdingsView());
      renderPage(createTestQueryClient(), false, '/investments?tab=trades');
      await screen.findByRole('table', { name: 'Closed trades' });
      expect(syncTrades).toHaveBeenCalledTimes(1);

      await user.click(tab('Holdings'));
      first.resolve(tradeStatus({ isComplete: false }));
      await first.promise;
      await new Promise((resolve) => setTimeout(resolve, 0));

      expect(syncTrades).toHaveBeenCalledTimes(1);
      expect(getTrades).toHaveBeenCalledTimes(1);
    });
  });
});
