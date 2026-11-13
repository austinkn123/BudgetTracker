import { AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { AxiosError } from 'axios';
import { fetchAuthSession, signOut } from 'aws-amplify/auth';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

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

describe('api failure handling', () => {
  beforeEach(() => {
    sent.length = 0;
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('on 401 logs the failure with its correlation id, signs out and redirects to /login', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    // jsdom does not implement navigation, so assigning location.href would throw a
    // "Not implemented" error; stub the global so the redirect target can be asserted.
    const location = { href: 'http://localhost/budgets' };
    vi.stubGlobal('location', location);
    respondWith(401, { 'x-correlation-id': 'unauth-id' });

    await expect(api.get('/budgets')).rejects.toThrow();

    expect(consoleError).toHaveBeenCalledTimes(1);
    const logged = JSON.stringify(consoleError.mock.calls[0]);
    expect(logged).toContain('(401)');
    expect(logged).toContain('unauth-id');
    expect(signOut).toHaveBeenCalledTimes(1);
    expect(window.location.href).toBe('/login');
  });

  it('does not sign out on a non-401 failure', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.mocked(signOut).mockClear();
    respondWith(403);

    await expect(api.get('/budgets')).rejects.toThrow();

    expect(signOut).not.toHaveBeenCalled();
  });

  it('logs the error code and the sent correlation id when there is no response', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    api.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
      sent.push(config);
      throw new AxiosError('Network Error', AxiosError.ERR_NETWORK, config, null, undefined);
    };

    await expect(api.post('/transactions?x=1', { payee: 'Acme' })).rejects.toThrow();

    const sentId = String(correlationIdOf(sent[0]));
    expect(consoleError).toHaveBeenCalledTimes(1);
    expect(consoleError.mock.calls[0][0]).toBe(
      `API POST /transactions failed (ERR_NETWORK) correlationId=${sentId}`
    );
  });

  it("logs 'network error' when there is neither a response nor an error code", async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    api.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
      sent.push(config);
      throw new AxiosError('Something failed', undefined, config, null, undefined);
    };

    await expect(api.get('/accounts')).rejects.toThrow();

    const sentId = String(correlationIdOf(sent[0]));
    expect(consoleError.mock.calls[0][0]).toBe(
      `API GET /accounts failed (network error) correlationId=${sentId}`
    );
  });

  it('never writes the ID token or Authorization header to console.error', async () => {
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.mocked(fetchAuthSession).mockResolvedValue({
      tokens: { idToken: { toString: () => 'SECRET-ID-TOKEN' } },
    } as unknown as Awaited<ReturnType<typeof fetchAuthSession>>);
    vi.stubGlobal('location', { href: 'http://localhost/' });

    respondWith(500);
    await expect(api.get('/accounts')).rejects.toThrow();
    respondWith(401);
    await expect(api.get('/accounts')).rejects.toThrow();
    api.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
      sent.push(config);
      throw new AxiosError('Network Error', AxiosError.ERR_NETWORK, config, null, undefined);
    };
    await expect(api.get('/accounts')).rejects.toThrow();

    // Guard against a vacuous pass: the token really was attached to the requests.
    expect(sent).toHaveLength(3);
    for (const config of sent) {
      expect(AxiosHeaders.from(config.headers).get('Authorization')).toBe('Bearer SECRET-ID-TOKEN');
    }
    expect(consoleError).toHaveBeenCalledTimes(3);
    const logged = consoleError.mock.calls
      .flat()
      .map((arg) => (typeof arg === 'string' ? arg : JSON.stringify(arg)))
      .join('\n');
    expect(logged).not.toContain('SECRET-ID-TOKEN');
    expect(logged).not.toMatch(/authorization/i);
    expect(logged).not.toMatch(/bearer/i);
  });
});
