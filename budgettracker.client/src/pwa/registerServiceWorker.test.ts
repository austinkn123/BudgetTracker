import { describe, expect, it, vi } from 'vitest';
import { registerServiceWorker } from './registerServiceWorker';

function fakeEnvironment(readyState: DocumentReadyState = 'complete') {
  const update = vi.fn(async () => undefined);
  const register = vi.fn(async () => ({ update }));
  const nav = { serviceWorker: { register } } as unknown as Navigator;
  const doc = new EventTarget() as Document;
  Object.defineProperty(doc, 'readyState', { value: readyState });
  let visibility: DocumentVisibilityState = 'visible';
  Object.defineProperty(doc, 'visibilityState', { get: () => visibility });
  const setVisibility = (state: DocumentVisibilityState) => {
    visibility = state;
    doc.dispatchEvent(new Event('visibilitychange'));
  };
  return { nav, doc, register, update, setVisibility };
}

const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('registerServiceWorker', () => {
  it('registers /sw.js for the whole site in a production build', async () => {
    const env = fakeEnvironment();

    registerServiceWorker(true, env.nav, env.doc);
    await flush();

    expect(env.register).toHaveBeenCalledWith('/sw.js', { scope: '/', updateViaCache: 'none' });
  });

  it('does nothing in development', () => {
    const env = fakeEnvironment();

    registerServiceWorker(false, env.nav, env.doc);

    expect(env.register).not.toHaveBeenCalled();
  });

  it('does nothing where service workers are unsupported', () => {
    const env = fakeEnvironment();

    expect(() => registerServiceWorker(true, {} as Navigator, env.doc)).not.toThrow();
  });

  it('waits for the page load before registering', async () => {
    const env = fakeEnvironment('loading');

    registerServiceWorker(true, env.nav, env.doc);
    await flush();
    expect(env.register).not.toHaveBeenCalled();

    window.dispatchEvent(new Event('load'));
    await flush();
    expect(env.register).toHaveBeenCalledTimes(1);
  });

  it('checks for a new version each time the app comes back to the foreground', async () => {
    const env = fakeEnvironment();
    registerServiceWorker(true, env.nav, env.doc);
    await flush();

    env.setVisibility('hidden');
    expect(env.update).not.toHaveBeenCalled();
    env.setVisibility('visible');
    expect(env.update).toHaveBeenCalledTimes(1);
  });

  it('keeps the app running when registration fails', async () => {
    const env = fakeEnvironment();
    env.register.mockRejectedValueOnce(new Error('blocked'));
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);

    registerServiceWorker(true, env.nav, env.doc);
    await flush();

    expect(warn).toHaveBeenCalledWith('Service worker registration failed', expect.any(Error));
  });
});
