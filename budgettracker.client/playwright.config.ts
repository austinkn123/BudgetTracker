import { defineConfig, devices } from '@playwright/test';
import { E2E_CLIENT_ID } from './e2e/support/session';

const PORT = 4174;

/** Layout checks against the real app with every /api call mocked (see e2e/support). No backend needed. */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? 'github' : 'list',
  use: { baseURL: `http://localhost:${PORT}`, trace: 'off' },
  projects: [
    // Dark is the default theme, so the light project saves a light choice the way the Settings toggle would.
    {
      name: 'iphone-se-light',
      use: {
        ...devices['iPhone SE (3rd gen)'],
        browserName: 'chromium',
        colorScheme: 'light',
        storageState: { cookies: [], origins: [{ origin: `http://localhost:${PORT}`, localStorage: [{ name: 'bud-theme', value: 'light' }] }] },
      },
    },
    { name: 'iphone-14-dark', use: { ...devices['iPhone 14'], browserName: 'chromium', colorScheme: 'dark' } },
  ],
  webServer: {
    command: `npx vite --port ${PORT} --strictPort`,
    url: `http://localhost:${PORT}`,
    reuseExistingServer: !process.env.CI,
    env: {
      E2E: '1',
      VITE_COGNITO_REGION: 'us-east-1',
      VITE_COGNITO_USER_POOL_ID: 'us-east-1_E2E',
      VITE_COGNITO_USER_POOL_CLIENT_ID: E2E_CLIENT_ID,
    },
  },
});
