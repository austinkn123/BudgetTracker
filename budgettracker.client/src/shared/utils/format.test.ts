import { describe, expect, it } from 'vitest';
import {
  inCurrency,
  localDateTime,
  localTime,
  overAmount,
  percentPrecise,
  quantity,
  signedCurrency,
  unitPrice,
  withSign,
} from './format';

describe('overAmount', () => {
  it('keeps cents on small overages so they never read as $0', () => {
    expect(overAmount(0.4)).toBe('$0.40');
    expect(overAmount(9.99)).toBe('$9.99');
  });

  it('drops cents once the overage is large enough to read whole', () => {
    expect(overAmount(88.12)).toBe('$88');
  });
});

describe('signedCurrency', () => {
  it('shows direction on whole-dollar roll-ups', () => {
    expect(signedCurrency(1200)).toBe('+$1,200');
    expect(signedCurrency(-80)).toBe('−$80');
    expect(signedCurrency(0)).toBe('+$0');
  });
});

describe('inCurrency', () => {
  it("formats in the account's own currency, with its cents", () => {
    expect(inCurrency(12345.67, 'USD')).toBe('$12,345.67');
    expect(inCurrency(2000, 'CAD')).toBe('CA$2,000.00');
  });

  it('falls back to the bare figure for a code Intl does not know, instead of throwing', () => {
    expect(inCurrency(5, 'not-a-code')).toBe('not-a-code 5.00');
  });
});

describe('local date and time', () => {
  // vitest.config pins TZ to America/New_York, where 18:04Z on Oct 22 is 2:04 PM.
  it('renders an ISO instant in the local zone', () => {
    expect(localDateTime('2026-10-22T18:04:11Z')).toMatch(/^Oct 22, 2026, 2:04\sPM$/);
    expect(localTime('2026-10-22T18:04:11Z')).toMatch(/^2:04\sPM$/);
  });
});

describe('unitPrice', () => {
  it.each([
    [531.378333, '$531.38'],
    [100000, '$100,000.00'],
    [1, '$1.00'],
    [0.5, '$0.50'],
    [0.000012, '$0.000012'],
  ])('shows cents from $1 up and keeps sub-dollar digits: %s → %s', (amount, expected) => {
    expect(unitPrice(amount, 'USD')).toBe(expected);
  });

  it("uses the account's own currency", () => {
    expect(unitPrice(531.378333, 'CAD')).toBe('CA$531.38');
  });
});

describe('percentPrecise', () => {
  it('shows percent units to 2 dp, so a column can be checked to sum to 100', () => {
    expect(percentPrecise(59.09)).toBe('59.09%');
    expect(percentPrecise(30)).toBe('30.00%');
    expect(percentPrecise(1234.5)).toBe('1,234.50%');
  });
});

describe('withSign', () => {
  it('puts + on gains, − on losses, and nothing on zero', () => {
    expect(withSign(1495, (v) => inCurrency(v, 'USD'))).toBe('+$1,495.00');
    expect(withSign(-42.1, (v) => inCurrency(v, 'USD'))).toBe('−$42.10');
    expect(withSign(0, percentPrecise)).toBe('0.00%');
  });
});

describe('quantity', () => {
  it('shows whole shares plainly and keeps crypto fractions', () => {
    expect(quantity(20)).toBe('20');
    expect(quantity(0.05)).toBe('0.05');
    expect(quantity(0.12345678)).toBe('0.12345678');
    expect(quantity(1500)).toBe('1,500');
  });
});
