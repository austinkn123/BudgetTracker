import { describe, expect, it } from 'vitest';
import { buildListQuery, statusToQuery } from './transactionFilters';

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
