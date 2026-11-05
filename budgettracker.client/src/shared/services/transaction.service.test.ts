import type { InternalAxiosRequestConfig } from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Capture the fully resolved request URL (base + path + query string) without touching the network.
const captured = vi.hoisted(() => ({ urls: [] as string[] }));

vi.mock('../api', async () => {
  const { default: axios } = await import('axios');
  const instance = axios.create({ baseURL: '/api' });
  instance.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
    captured.urls.push(instance.getUri(config));
    return { data: { items: [], nextCursor: null }, status: 200, statusText: 'OK', headers: {}, config };
  };
  return { default: instance };
});

import { categoryService } from './category.service';
import { transactionService } from './transaction.service';

describe('transaction reads', () => {
  beforeEach(() => {
    captured.urls.length = 0;
  });

  it('pages with the filters, cursor and limit, dropping anything unset', async () => {
    await transactionService.getPage({ uncategorized: true, search: '' }, { cursor: 'abc', limit: 50 });

    expect(captured.urls).toEqual(['/api/transactions/page?uncategorized=true&cursor=abc&limit=50']);
  });

  it('asks for the first page with no cursor at all', async () => {
    await transactionService.getPage({}, { limit: 50 });

    expect(captured.urls).toEqual(['/api/transactions/page?limit=50']);
  });

  it('reads the summary', async () => {
    await transactionService.getSummary();

    expect(captured.urls).toEqual(['/api/transactions/summary']);
  });

  it('reads category usage since a date', async () => {
    await categoryService.getUsage('2025-11-06');

    expect(captured.urls).toEqual(['/api/categories/usage?since=2025-11-06']);
  });

  it('refuses a filtered read with no filter, before it reaches the server', async () => {
    await expect(transactionService.getFiltered({ search: '' })).rejects.toThrow(/at least one filter/);
    expect(captured.urls).toEqual([]);
  });

  it('sends a bounded read with its filters', async () => {
    await transactionService.getFiltered({ from: '2026-04-01', to: '2026-04-30' });

    expect(captured.urls).toEqual(['/api/transactions?from=2026-04-01&to=2026-04-30']);
  });

  it('has no unfiltered ledger read left to call', () => {
    expect(transactionService).not.toHaveProperty('getCurrentUserTransactions');
  });
});
