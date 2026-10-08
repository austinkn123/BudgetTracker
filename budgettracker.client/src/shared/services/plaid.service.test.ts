import type { InternalAxiosRequestConfig } from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Capture the fully resolved request URL (base + path + query string) without touching the network.
const captured = vi.hoisted(() => ({ urls: [] as string[] }));

vi.mock('../api', async () => {
  const { default: axios } = await import('axios');
  const instance = axios.create({ baseURL: '/api' });
  instance.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
    captured.urls.push(instance.getUri(config));
    return {
      data: { inserted: 0, updated: 0, removed: 0, syncedAt: '2026-09-27T00:00:00Z' },
      status: 200,
      statusText: 'OK',
      headers: {},
      config,
    };
  };
  return { default: instance };
});

import { plaidService } from './plaid.service';

describe('plaidService.sync', () => {
  beforeEach(() => {
    captured.urls.length = 0;
  });

  it('sends no staleAfterHours param when called without an argument (manual refresh)', async () => {
    await plaidService.sync();

    expect(captured.urls).toEqual(['/api/plaid/sync']);
  });

  it('sends ?staleAfterHours=6 when called with 6 (sync-on-open)', async () => {
    await plaidService.sync(6);

    expect(captured.urls).toEqual(['/api/plaid/sync?staleAfterHours=6']);
  });
});
