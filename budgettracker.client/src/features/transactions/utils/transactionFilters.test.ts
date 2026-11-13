import { describe, expect, it } from 'vitest';
import { ALLY, CHASE } from '../../../test/plaidFixtures';
import type { PlaidConnectionView } from '../../../shared/types/api';
import {
  accountOptionsFrom,
  buildCalendarQuery,
  buildFeedQuery,
  buildListQuery,
  resolveAccountFilter,
  statusToQuery,
} from './transactionFilters';

describe('statusToQuery', () => {
  it('sends no constraints for "all"', () => {
    expect(statusToQuery('all')).toEqual({});
  });

  it('asks only for uncategorized rows', () => {
    expect(statusToQuery('uncategorized')).toEqual({ uncategorized: true });
  });

  it('maps pending to its own flag', () => {
    expect(statusToQuery('pending')).toEqual({ isPending: true });
  });
});

describe('buildListQuery', () => {
  // Local dates, matching what startOfMonth/endOfMonth actually hand this function. Constructing
  // UTC instants here would test a case the app never produces — and would hide the very bug
  // calendar-date bounds exist to prevent.
  const monthStart = new Date(2026, 3, 1);
  const monthEnd = new Date(2026, 3, 30, 23, 59, 59, 999);

  it('bounds the query to the visible month as calendar dates', () => {
    const query = buildListQuery('all', '', monthStart, monthEnd);

    expect(query.from).toBe('2026-04-01');
    expect(query.to).toBe('2026-04-30');
  });

  it('never emits an instant, which would shift the window outside UTC', () => {
    const query = buildListQuery('all', '', monthStart, monthEnd);

    // OccurredAt is a timezone-free calendar date server-side. An ISO instant from a UTC-5
    // browser would start April at 05:00Z, dropping an April 1 row and admitting a May 1 one.
    expect(query.from).not.toContain('T');
    expect(query.to).not.toContain('T');
    expect(query.from).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });

  it('keeps the local calendar day regardless of the time component', () => {
    // 23:59 local on the last of the month must still serialise as that day, not the next.
    expect(buildListQuery('all', '', monthStart, new Date(2026, 3, 30, 23, 59)).to).toBe('2026-04-30');
  });

  it('combines the status flag with the search term', () => {
    const query = buildListQuery('uncategorized', 'grocer', monthStart, monthEnd);

    expect(query.uncategorized).toBe(true);
    expect(query.search).toBe('grocer');
  });

  it('omits search entirely when it is blank or whitespace', () => {
    expect(buildListQuery('all', '   ', monthStart, monthEnd)).not.toHaveProperty('search');
    expect(buildListQuery('all', '', monthStart, monthEnd)).not.toHaveProperty('search');
  });

  it('trims the search term so a stray space does not change the query key', () => {
    expect(buildListQuery('all', '  H-E-B  ', monthStart, monthEnd).search).toBe('H-E-B');
  });
});

describe('buildFeedQuery', () => {
  it('carries the chip and the search but no dates, so the feed spans all history', () => {
    expect(buildFeedQuery('pending', '  coffee ')).toEqual({ isPending: true, search: 'coffee' });
  });

  it('is empty for "all" with no search', () => {
    expect(buildFeedQuery('all', '   ')).toEqual({});
  });

  it('sends no account constraint when every account is shown', () => {
    expect(buildFeedQuery('all', '', null)).toEqual({});
  });

  it('narrows to one linked account alongside the other filters', () => {
    expect(buildFeedQuery('pending', 'grocer', 'acc-chase-1')).toEqual({
      isPending: true,
      search: 'grocer',
      plaidAccountId: 'acc-chase-1',
    });
  });
});

describe('accountOptionsFrom', () => {
  const CHASE_TWO: PlaidConnectionView = {
    ...CHASE,
    accounts: [
      ...CHASE.accounts,
      { plaidAccountId: 'acc-chase-2', name: 'Sapphire', mask: '9876', accountType: 'credit' },
    ],
  };

  it('lists every account across every connection, labelled by institution and mask', () => {
    expect(accountOptionsFrom([CHASE_TWO, ALLY])).toEqual([
      { value: 'acc-chase-1', label: 'Chase ••1234' },
      { value: 'acc-chase-2', label: 'Chase ••9876' },
      { value: 'acc-ally-1', label: 'Ally Bank · Savings' },
    ]);
  });

  it('is empty before connections load', () => {
    expect(accountOptionsFrom(undefined)).toEqual([]);
  });
});

describe('resolveAccountFilter', () => {
  const options = accountOptionsFrom([CHASE, ALLY]);

  it('keeps an account that is still linked', () => {
    expect(resolveAccountFilter('acc-ally-1', options)).toBe('acc-ally-1');
  });

  it('falls back to all accounts once the selected bank is disconnected', () => {
    expect(resolveAccountFilter('acc-gone', options)).toBeNull();
  });

  it('passes "all accounts" through', () => {
    expect(resolveAccountFilter(null, options)).toBeNull();
  });
});

describe('buildCalendarQuery', () => {
  it('covers the six fixed weeks the grid draws, outside days included', () => {
    // April 2026 starts on a Wednesday: the grid opens on Sunday March 29 and runs 42 days.
    expect(buildCalendarQuery(new Date(2026, 3, 1))).toEqual({ from: '2026-03-29', to: '2026-05-09' });
  });

  it('anchors on the month of any day passed in', () => {
    expect(buildCalendarQuery(new Date(2026, 3, 17))).toEqual(buildCalendarQuery(new Date(2026, 3, 1)));
  });

  it('still spans six weeks for a month that starts on a Sunday', () => {
    // November 2026 starts on a Sunday, so no leading days from October.
    expect(buildCalendarQuery(new Date(2026, 10, 1))).toEqual({ from: '2026-11-01', to: '2026-12-12' });
  });
});
