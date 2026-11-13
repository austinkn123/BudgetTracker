import type { InternalAxiosRequestConfig } from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const captured = vi.hoisted(() => ({ requests: [] as { method?: string; url: string }[] }));

vi.mock('../api', async () => {
  const { default: axios } = await import('axios');
  const instance = axios.create({ baseURL: '/api' });
  instance.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
    captured.requests.push({ method: config.method, url: instance.getUri(config) });
    return { data: { version: 42 }, status: 200, statusText: 'OK', headers: {}, config };
  };
  return { default: instance };
});

import { dataVersionService } from './dataVersion.service';

describe('dataVersionService.getVersion', () => {
  beforeEach(() => {
    captured.requests.length = 0;
  });

  it('GETs /api/data-version and returns the body', async () => {
    await expect(dataVersionService.getVersion()).resolves.toEqual({ version: 42 });
    expect(captured.requests).toEqual([{ method: 'get', url: '/api/data-version' }]);
  });
});
