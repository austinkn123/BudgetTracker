/* Service worker for the installable app (PWA).
 *
 * Plain JS on purpose: the build emits this file as /sw.js with __BUILD_ID__ replaced by a hash of the
 * build's file names (swBuildPlugin in vite.config.ts), so every deploy ships a byte-different worker and
 * browsers install it. Covered by src/pwa/sw.test.ts.
 *
 * What it caches, and only this: the app shell (index.html, manifest, theme script, icons) and the
 * content-hashed /assets/* files (JS, CSS, fonts). None of these hold account or bank data.
 * What it never touches: any request that isn't a same-origin GET, /api/* and /health* (the Lambda, where
 * every account and bank response comes from), and /plaid-oauth (a bank's OAuth return; it always goes
 * straight to the network). Cross-origin requests (Cognito sign-in, Plaid Link) are left to the browser.
 */
const BUILD_ID = '__BUILD_ID__';
const CACHE_PREFIX = 'bt-';
const SHELL_CACHE = `${CACHE_PREFIX}shell-${BUILD_ID}`;
const ASSET_CACHE = `${CACHE_PREFIX}assets-${BUILD_ID}`;
const SHELL_URLS = [
  '/',
  '/manifest.webmanifest',
  '/theme-init.js',
  '/icons/icon-192.png',
  '/icons/apple-touch-icon.png',
];
const NETWORK_ONLY_PREFIXES = ['/api/', '/health', '/plaid-oauth'];

const isNetworkOnly = (pathname) =>
  pathname === '/api' || NETWORK_ONLY_PREFIXES.some((prefix) => pathname.startsWith(prefix));

self.addEventListener('install', (event) => {
  // cache: 'reload' skips the HTTP cache so the shell matches this build. A new worker takes over at
  // once; open pages keep running the code they loaded and pick up the new build on their next load.
  event.waitUntil(
    caches
      .open(SHELL_CACHE)
      .then((cache) =>
        cache.addAll(SHELL_URLS.map((url) => new Request(new URL(url, self.location.href), { cache: 'reload' }))),
      )
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  // Drop every earlier build's caches, so old assets never pile up.
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(
          keys
            .filter((key) => key.startsWith(CACHE_PREFIX) && key !== SHELL_CACHE && key !== ASSET_CACHE)
            .map((key) => caches.delete(key)),
        ),
      )
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (isNetworkOnly(url.pathname)) return;

  if (request.mode === 'navigate') {
    // Network first, so an online launch always gets the newest index.html (and with it the newest
    // build). The cached shell is only the offline fallback; the page is never written to the cache.
    event.respondWith(fetch(request).catch(() => caches.match('/', { cacheName: SHELL_CACHE })));
    return;
  }

  if (url.pathname.startsWith('/assets/')) {
    // File names carry a content hash, so a cached copy is always the right one.
    event.respondWith(
      caches.open(ASSET_CACHE).then((cache) =>
        cache.match(request).then(
          (cached) =>
            cached ||
            fetch(request).then((response) => {
              if (response.ok && response.type === 'basic') cache.put(request, response.clone());
              return response;
            }),
        ),
      ),
    );
    return;
  }

  if (SHELL_URLS.includes(url.pathname)) {
    // Shell files: network first, cached copy when offline.
    event.respondWith(fetch(request).catch(() => caches.match(url.pathname, { cacheName: SHELL_CACHE })));
  }
  // Anything else goes to the network as if there were no service worker.
});
