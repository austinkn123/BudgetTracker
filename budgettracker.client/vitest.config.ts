import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vitest/config';

/**
 * Standalone test config. Kept separate from vite.config.ts so running tests doesn't trigger the
 * dev-server HTTPS certificate export (dotnet dev-certs) that the dev config performs on load.
 * No React plugin: Vitest's built-in transform already compiles TSX with the automatic runtime.
 */
export default defineConfig({
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
  },
});
