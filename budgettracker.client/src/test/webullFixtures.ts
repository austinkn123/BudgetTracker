import type {
  WebullAccountHoldings,
  WebullBalance,
  WebullConnectionView,
  WebullHoldingsView,
  WebullClosedTrade,
  WebullTradeHistoryStatus,
  WebullTradeSummary,
  WebullTradesView,
} from '../shared/types/api';

export const CASH_ACCOUNT: WebullBalance = {
  accountId: 'LOJO-cash',
  accountLabel: 'Individual Cash',
  accountType: 'CASH',
  totalNetLiquidationValue: 12345.67,
  totalCashBalance: 500,
  currency: 'USD',
};

/** No label (falls back to the type), no cash figure, and not in dollars. */
export const MARGIN_ACCOUNT: WebullBalance = {
  accountId: 'LOJO-margin',
  accountLabel: null,
  accountType: 'MARGIN',
  totalNetLiquidationValue: 2000,
  totalCashBalance: null,
  currency: 'CAD',
};

export const webullView = (overrides: Partial<WebullConnectionView> = {}): WebullConnectionView => ({
  state: 'Connected',
  balances: [CASH_ACCOUNT],
  asOf: '2026-10-22T18:04:11Z',
  isStale: false,
  lastError: null,
  approvalExpiresAt: null,
  canVerify: false,
  ...overrides,
});

/**
 * Hand-written test data, not a real pull (production so far is one MARGIN account of EQUITY ETFs).
 * Covers a crypto row, a stock row, and an EVENT row whose computed figures are all null, which
 * is what exercises the "—" rendering.
 */
export const HOLDINGS_ACCOUNT: WebullAccountHoldings = {
  accountId: 'ACC1',
  accountLabel: 'Individual Cash',
  accountType: 'CASH',
  currency: 'USD',
  totalValue: 12500,
  cash: 1500,
  dayProfitLoss: -42.1,
  openProfitLoss: 2995,
  openProfitLossPercent: 37.41,
  positions: [
    {
      symbol: 'BTCUSD',
      instrumentType: 'CRYPTO',
      quantity: 0.05,
      averageCost: 100000,
      totalCost: 5000,
      marketValue: 6500,
      openProfitLoss: 1500,
      openProfitLossPercent: 30,
      percentOfHoldings: 59.09,
    },
    {
      symbol: 'AAPL',
      instrumentType: 'EQUITY',
      quantity: 20,
      averageCost: 150.25,
      totalCost: 3005,
      marketValue: 4500,
      openProfitLoss: 1495,
      openProfitLossPercent: 49.75,
      percentOfHoldings: 40.91,
    },
    {
      symbol: 'XYZ',
      instrumentType: 'EVENT',
      quantity: 1,
      averageCost: null,
      totalCost: null,
      marketValue: null,
      openProfitLoss: null,
      openProfitLossPercent: null,
      percentOfHoldings: null,
    },
  ],
};

export const holdingsView = (overrides: Partial<WebullHoldingsView> = {}): WebullHoldingsView => ({
  state: 'Connected',
  asOf: '2026-10-23T20:31:04Z',
  isStale: false,
  lastError: null,
  accounts: [HOLDINGS_ACCOUNT],
  ...overrides,
});

export const tradeStatus = (
  overrides: Partial<WebullTradeHistoryStatus> = {},
): WebullTradeHistoryStatus => ({
  state: 'Connected',
  asOf: '2026-10-24T20:05:00Z',
  historyFrom: '2024-10-24',
  isComplete: true,
  lastError: null,
  ...overrides,
});

/**
 * Server order (newest close first). A loss, a crypto gain, and a gain whose percent is null.
 * AAPL closes on Jan 2: shifted through UTC it would read Jan 1 in New York, the test zone.
 */
export const CLOSED_TRADES: WebullClosedTrade[] = [
  {
    accountId: 'ACC1',
    orderId: 'ORD-3',
    closeDate: '2026-10-01',
    symbol: 'TSLA',
    instrumentType: 'EQUITY',
    quantity: 10,
    averageCost: 250.5,
    salePrice: 220.25,
    realizedProfitLoss: -302.5,
    realizedProfitLossPercent: -12.08,
  },
  {
    accountId: 'ACC1',
    orderId: 'ORD-2',
    closeDate: '2026-06-15',
    symbol: 'BTCUSD',
    instrumentType: 'CRYPTO',
    quantity: 0.01,
    averageCost: 60000,
    salePrice: 70000,
    realizedProfitLoss: 100,
    realizedProfitLossPercent: 16.67,
  },
  {
    accountId: 'ACC1',
    orderId: 'ORD-1',
    closeDate: '2026-01-02',
    symbol: 'AAPL',
    instrumentType: 'EQUITY',
    quantity: 5,
    averageCost: 150,
    salePrice: 250,
    realizedProfitLoss: 500,
    realizedProfitLossPercent: null,
  },
];

/** Deliberately not the sum of the rows' figures, so a test can tell server figures from client maths. */
export const TRADE_SUMMARY: WebullTradeSummary = {
  totalRealizedProfitLoss: 297.25,
  wins: 2,
  losses: 1,
  winRate: 66.67,
  largestWin: 500,
  largestLoss: -302.5,
  excludedIncompleteCost: 0,
  excludedUnsupported: 0,
};

/** What a range with no closed trades reports. */
export const EMPTY_TRADE_SUMMARY: WebullTradeSummary = {
  totalRealizedProfitLoss: 0,
  wins: 0,
  losses: 0,
  winRate: null,
  largestWin: null,
  largestLoss: null,
  excludedIncompleteCost: 0,
  excludedUnsupported: 0,
};

export const tradesView = (overrides: Partial<WebullTradesView> = {}): WebullTradesView => ({
  history: tradeStatus(),
  from: '2026-01-01',
  to: '2026-10-24',
  summary: TRADE_SUMMARY,
  trades: CLOSED_TRADES,
  ...overrides,
});
