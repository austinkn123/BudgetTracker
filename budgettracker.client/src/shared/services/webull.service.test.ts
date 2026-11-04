import type { InternalAxiosRequestConfig } from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// Capture method + resolved URL without touching the network; `net.status` decides the response.
const net = vi.hoisted(() => ({ calls: [] as string[], bodies: [] as unknown[], status: 200 }));

vi.mock('../api', async () => {
  const { default: axios } = await import('axios');
  const instance = axios.create({ baseURL: '/api' });
  instance.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
    net.calls.push(`${config.method?.toUpperCase()} ${instance.getUri(config)}`);
    net.bodies.push(config.data);
    const data = net.status === 200 ? { state: 'Connected' } : { error: 'Nothing to verify.' };
    const response = { data, status: net.status, statusText: '', headers: {}, config };
    if (net.status !== 200) {
      throw Object.assign(new Error(`Request failed with status code ${net.status}`), { response });
    }
    return response;
  };
  return { default: instance };
});

import { webullService } from './webull.service';

describe('webullService', () => {
  beforeEach(() => {
    net.calls.length = 0;
    net.bodies.length = 0;
    net.status = 200;
  });

  it('reads, refreshes and verifies against the contract routes with no body', async () => {
    await expect(webullService.get()).resolves.toEqual({ state: 'Connected' });
    await expect(webullService.refresh()).resolves.toEqual({ state: 'Connected' });
    await expect(webullService.verify()).resolves.toEqual({ state: 'Connected' });
    await expect(webullService.getHoldings()).resolves.toEqual({ state: 'Connected' });

    expect(net.calls).toEqual([
      'GET /api/webull',
      'POST /api/webull/refresh',
      'POST /api/webull/verify',
      'GET /api/webull/holdings',
    ]);
    expect(net.bodies).toEqual([undefined, undefined, undefined, undefined]);
  });

  it('reads trades for the chosen range and syncs them with no body', async () => {
    await expect(webullService.getTrades('1M')).resolves.toEqual({ state: 'Connected' });
    await expect(webullService.getTrades('All')).resolves.toEqual({ state: 'Connected' });
    await expect(webullService.syncTrades()).resolves.toEqual({ state: 'Connected' });

    expect(net.calls).toEqual([
      'GET /api/webull/trades?range=1M',
      'GET /api/webull/trades?range=All',
      'POST /api/webull/trades/sync',
    ]);
    expect(net.bodies).toEqual([undefined, undefined, undefined]);
  });

  it('rejects verify on a 409, keeping the server error for the UI', async () => {
    net.status = 409;

    await expect(webullService.verify()).rejects.toMatchObject({
      response: { status: 409, data: { error: 'Nothing to verify.' } },
    });
  });
});
