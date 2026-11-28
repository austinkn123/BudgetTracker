/**
 * Registers /sw.js (src/pwa/sw.js) so the app can be installed to a phone's home screen.
 *
 * Production builds only: the dev server has no /sw.js, and a worker left over from a build would serve
 * stale code during development. `updateViaCache: 'none'` makes the browser fetch the worker script
 * past its HTTP cache on every update check, and a check runs each time the app comes back to the
 * foreground, because an installed app on a phone is resumed far more often than it is reloaded.
 */
export function registerServiceWorker(
  isProduction: boolean = import.meta.env.PROD,
  nav: Navigator = navigator,
  doc: Document = document,
): void {
  if (!isProduction || !('serviceWorker' in nav)) return;

  const register = () => {
    nav.serviceWorker
      .register('/sw.js', { scope: '/', updateViaCache: 'none' })
      .then((registration) => {
        doc.addEventListener('visibilitychange', () => {
          if (doc.visibilityState === 'visible') registration.update().catch(() => undefined);
        });
      })
      .catch((error: unknown) => {
        // The app works the same without the worker; it just can't be installed or open offline.
        console.warn('Service worker registration failed', error);
      });
  };

  // Wait for the page to finish loading so the worker's shell download doesn't compete with it.
  if (doc.readyState === 'complete') register();
  else window.addEventListener('load', register, { once: true });
}
