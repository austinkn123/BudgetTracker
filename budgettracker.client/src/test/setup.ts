import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// Vitest runs without globals, so RTL can't register its own auto-cleanup.
afterEach(() => {
  cleanup();
});
