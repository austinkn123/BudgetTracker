import { AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('aws-amplify/auth', () => ({
  fetchAuthSession: vi.fn(async () => ({ tokens: undefined })),
  signOut: vi.fn(async () => undefined),
}));

import api from './api';

const sent: InternalAxiosRequestConfig[] = [];

function respondWith(status: number, headers: Record<string, string> = {}, data: unknown = {}) {
  api.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
    sent.push(config);
    const response = { data, status, statusText: String(status), headers, config };
    if (status >= 400) {
      const { AxiosError } = await import('axios');
      throw new AxiosError('Request failed', 'ERR_BAD_RESPONSE', config, null, response);
    }
    return response;
  };
}

const correlationIdOf = (config: InternalAxiosRequestConfig) =>
  AxiosHeaders.from(config.headers).get('X-Correlation-ID');

describe('api correlation id', () => {
  beforeEach(() => {
    sent.length = 0;
  });

  it('sends an X-Correlation-ID header that is a fresh UUID on every request', async () => {
    respondWith(200);

    await api.get('/accounts');
    await api.get('/accounts');

    const ids = sent.map(correlationIdOf);
    expect(ids).toHaveLength(2);
    for (const id of ids) {
      expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i);
    }
    expect(ids[0]).not.toBe(ids[1]);
  });

  it('keeps a correlation id the caller already set', async () => {
    respondWith(200);

    await api.get('/accounts', { headers: { 'X-Correlation-ID': 'caller-supplied' } });

    expect(correlationIdOf(sent[0])).toBe('caller-supplied');
  });

  it('logs the server-echoed correlation id on failure, without the body or query string', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    respondWith(500, { 'x-correlation-id': 'server-echoed' }, { detail: 'Payee Acme $42.00' });

    await expect(api.get('/transactions?search=Acme')).rejects.toThrow();

    expect(consoleError).toHaveBeenCalledTimes(1);
    const logged = JSON.stringify(consoleError.mock.calls[0]);
    expect(logged).toContain('server-echoed');
    expect(logged).toContain('/transactions');
    expect(logged).not.toContain('Acme');
    expect(logged).not.toContain('42.00');
  });

  it('falls back to the sent correlation id when the server does not echo one', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    respondWith(503);

    await expect(api.get('/accounts')).rejects.toThrow();

    const sentId = correlationIdOf(sent[0]);
    expect(sentId).toBeTruthy();
    expect(JSON.stringify(consoleError.mock.calls[0])).toContain(String(sentId));
  });
});
