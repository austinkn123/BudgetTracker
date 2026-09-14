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
  const monthStart = new Date('2026-04-01T00:00:00.000Z');
  const monthEnd = new Date('2026-04-30T23:59:59.999Z');

  it('always bounds the query to the visible month', () => {
    const query = buildListQuery('all', '', monthStart, monthEnd);

    expect(query.from).toBe(monthStart.toISOString());
    expect(query.to).toBe(monthEnd.toISOString());
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
