import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { beforeEach, describe, expect, it, vi } from 'vitest';

/**
 * Runs src/pwa/sw.js against a fake ServiceWorkerGlobalScope, so the rules about what it may cache
 * are checked without a browser.
 */
const SOURCE = readFileSync(join(import.meta.dirname, 'sw.js'), 'utf8');
const ORIGIN = 'https://budget.example';

type Handler = (event: unknown) => void;

function loadWorker(buildId = 'build-1') {
  const handlers: Record<string, Handler> = {};
  const stores = new Map<string, Map<string, Response>>();
  const keyOf = (input: Request | string) =>
    new URL(typeof input === 'string' ? input : input.url, ORIGIN).pathname;
  const openStore = (name: string) => {
    if (!stores.has(name)) stores.set(name, new Map());
    const store = stores.get(name)!;
    return {
      match: vi.fn(async (input: Request | string) => store.get(keyOf(input))?.clone()),
      put: vi.fn(async (input: Request | string, response: Response) => void store.set(keyOf(input), response)),
      addAll: vi.fn(async (inputs: Request[]) => {
        for (const input of inputs) store.set(keyOf(input), new Response(`cached ${keyOf(input)}`));
      }),
    };
  };
  const caches = {
    open: vi.fn(async (name: string) => openStore(name)),
    keys: vi.fn(async () => [...stores.keys()]),
    delete: vi.fn(async (name: string) => stores.delete(name)),
    match: vi.fn(async (input: string, options?: { cacheName?: string }) =>
      options?.cacheName ? stores.get(options.cacheName)?.get(keyOf(input))?.clone() : undefined,
    ),
  };
  const fetchMock = vi.fn(async (input: Request) => {
    const response = new Response(`network ${keyOf(input)}`, { status: 200 });
    Object.defineProperty(response, 'type', { value: 'basic' });
    return response;
  });
  const self = {
    location: new URL(ORIGIN),
    addEventListener: (type: string, handler: Handler) => {
      handlers[type] = handler;
    },
    skipWaiting: vi.fn(async () => undefined),
    clients: { claim: vi.fn(async () => undefined) },
  };

  new Function('self', 'caches', 'fetch', SOURCE.replaceAll('__BUILD_ID__', buildId))(self, caches, fetchMock);

  /** Dispatches a fetch; returns the response the worker chose, or null if it let the browser handle it. */
  const dispatchFetch = async (path: string, init: { method?: string; mode?: RequestMode } = {}) => {
    const url = path.startsWith('http') ? path : `${ORIGIN}${path}`;
    const request = { url, method: init.method ?? 'GET', mode: init.mode ?? 'cors' } as Request;
    const responded: { promise?: Promise<Response | undefined> } = {};
    handlers.fetch({ request, respondWith: (p: Promise<Response | undefined>) => (responded.promise = p) });
    return responded.promise ? ((await responded.promise) ?? null) : null;
  };

  const lifecycle = async (type: 'install' | 'activate') => {
    let work: Promise<unknown> = Promise.resolve();
    handlers[type]({ waitUntil: (p: Promise<unknown>) => (work = p) });
    await work;
  };

  return { self, caches, stores, fetchMock, dispatchFetch, lifecycle };
}

describe('service worker', () => {
  let worker: ReturnType<typeof loadWorker>;

  beforeEach(() => {
    worker = loadWorker();
  });

  it.each([
    '/api/transactions',
    '/api/plaid/accounts?itemId=4',
    '/api',
    '/health',
    '/health/ready',
    '/plaid-oauth?oauth_state_id=abc',
  ])('never handles %s, so account and bank data never reach a cache', async (path) => {
    expect(await worker.dispatchFetch(path)).toBeNull();
    expect(await worker.dispatchFetch(path, { mode: 'navigate' })).toBeNull();
    expect(worker.caches.open).not.toHaveBeenCalled();
  });

  it('leaves cross-origin requests (Cognito, Plaid) to the browser', async () => {
    expect(await worker.dispatchFetch('https://cognito-idp.us-east-2.amazonaws.com/')).toBeNull();
    expect(await worker.dispatchFetch('https://cdn.plaid.com/link/v2/stable/link-initialize.js')).toBeNull();
  });

  it('leaves anything but GET to the browser', async () => {
    expect(await worker.dispatchFetch('/assets/index-abc.js', { method: 'POST' })).toBeNull();
  });

  it('caches the app shell on install and takes over straight away', async () => {
    await worker.lifecycle('install');

    const shell = worker.stores.get('bt-shell-build-1')!;
    expect([...shell.keys()]).toEqual([
      '/',
      '/manifest.webmanifest',
      '/theme-init.js',
      '/icons/icon-192.png',
      '/icons/apple-touch-icon.png',
    ]);
    expect(worker.self.skipWaiting).toHaveBeenCalled();
  });

  it('loads pages from the network first and never caches them', async () => {
    const response = await worker.dispatchFetch('/budget', { mode: 'navigate' });

    expect(await response!.text()).toBe('network /budget');
    expect(worker.caches.open).not.toHaveBeenCalled();
  });

  it('opens the cached shell when a page load fails offline', async () => {
    await worker.lifecycle('install');
    worker.fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'));

    const response = await worker.dispatchFetch('/transactions', { mode: 'navigate' });

    expect(await response!.text()).toBe('cached /');
  });

  it('serves hashed assets from the cache after the first load', async () => {
    const first = await worker.dispatchFetch('/assets/index-abc.js');
    const second = await worker.dispatchFetch('/assets/index-abc.js');

    expect(await first!.text()).toBe('network /assets/index-abc.js');
    expect(await second!.text()).toBe('network /assets/index-abc.js');
    expect(worker.fetchMock).toHaveBeenCalledTimes(1);
  });

  it('does not cache a failed asset response', async () => {
    worker.fetchMock.mockResolvedValueOnce(new Response('missing', { status: 404 }));

    await worker.dispatchFetch('/assets/gone-123.js');

    expect(worker.stores.get('bt-assets-build-1')?.size ?? 0).toBe(0);
  });

  it("drops the previous build's caches when a new build activates", async () => {
    const old = loadWorker('build-1');
    await old.lifecycle('install');
    await old.dispatchFetch('/assets/index-old.js');

    const next = loadWorker('build-2');
    // Same browser cache storage: carry the old build's caches over, plus one this app doesn't own.
    for (const [name, store] of old.stores) next.stores.set(name, store);
    next.stores.set('someone-else', new Map());
    await next.lifecycle('install');
    await next.lifecycle('activate');

    expect([...next.stores.keys()].sort()).toEqual(['bt-shell-build-2', 'someone-else']);
    expect(next.self.clients.claim).toHaveBeenCalled();
  });
});
