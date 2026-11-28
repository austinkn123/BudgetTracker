import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

/** The install metadata: what Android, desktop Chrome and iPhone read to offer "Install" / "Add to Home Screen". */
const clientFile = (path: string) => join(import.meta.dirname, '..', '..', path);

interface ManifestIcon {
  src: string;
  sizes: string;
  type: string;
  purpose: string;
}

const manifest = JSON.parse(readFileSync(clientFile('public/manifest.webmanifest'), 'utf8')) as {
  name: string;
  short_name: string;
  start_url: string;
  scope: string;
  display: string;
  theme_color: string;
  background_color: string;
  icons: ManifestIcon[];
};
const html = new DOMParser().parseFromString(readFileSync(clientFile('index.html'), 'utf8'), 'text/html');
const headers = readFileSync(clientFile('public/_headers'), 'utf8');

/** Reads width and height from a PNG's IHDR chunk. */
const pngSize = (path: string) => {
  const bytes = readFileSync(clientFile(`public${path}`));
  return `${bytes.readUInt32BE(16)}x${bytes.readUInt32BE(20)}`;
};

describe('web app manifest', () => {
  it('opens the app full screen from the site root', () => {
    expect(manifest).toMatchObject({
      name: 'BudgetTracker',
      start_url: '/',
      scope: '/',
      display: 'standalone',
    });
    expect(manifest.short_name.length).toBeLessThanOrEqual(12);
  });

  it('uses the dark theme background so launch has no white flash', () => {
    expect(manifest.theme_color).toBe('#06121E');
    expect(manifest.background_color).toBe('#06121E');
    expect(html.querySelector('meta[name="theme-color"]')?.getAttribute('content')).toBe('#06121E');
  });

  it('lists 192 and 512 PNG icons, a maskable one included, that exist at their stated size', () => {
    const pngs = manifest.icons.filter((icon) => icon.type === 'image/png');
    expect(pngs.map((icon) => icon.sizes)).toEqual(expect.arrayContaining(['192x192', '512x512']));
    expect(manifest.icons.some((icon) => icon.purpose === 'maskable')).toBe(true);
    for (const icon of manifest.icons) expect(existsSync(clientFile(`public${icon.src}`))).toBe(true);
    for (const icon of pngs) expect(pngSize(icon.src)).toBe(icon.sizes);
  });
});

describe('index.html install tags', () => {
  it('links the manifest', () => {
    expect(html.querySelector('link[rel="manifest"]')?.getAttribute('href')).toBe('/manifest.webmanifest');
  });

  it('gives iPhone a 180px home screen icon and full screen mode', () => {
    const href = html.querySelector('link[rel="apple-touch-icon"]')?.getAttribute('href');
    expect(href).toBe('/icons/apple-touch-icon.png');
    expect(pngSize(href!)).toBe('180x180');
    expect(html.querySelector('meta[name="apple-mobile-web-app-capable"]')?.getAttribute('content')).toBe('yes');
    expect(html.querySelector('meta[name="apple-mobile-web-app-title"]')?.getAttribute('content')).toBe(
      manifest.short_name,
    );
  });

  it('uses an icon that exists for the favicon', () => {
    const href = html.querySelector('link[rel="icon"]')?.getAttribute('href');
    expect(existsSync(clientFile(`public${href}`))).toBe(true);
  });
});

describe('security headers', () => {
  it('let the manifest and service worker load from this site only', () => {
    expect(headers).toMatch(/manifest-src 'self'/);
    expect(headers).toMatch(/worker-src 'self'/);
  });

  it('never let /sw.js go stale in a cache', () => {
    expect(headers).toMatch(/\/sw\.js\n\s+Cache-Control: no-cache/);
  });
});
