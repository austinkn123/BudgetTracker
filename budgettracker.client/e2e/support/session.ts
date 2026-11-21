import type { Page, Route } from '@playwright/test';
import * as data from './fixtures';

/** Must match the VITE_COGNITO_USER_POOL_CLIENT_ID the e2e web server is started with (playwright.config.ts). */
export const E2E_CLIENT_ID = 'e2e-client-id';
const USERNAME = 'e2e-user';

const base64Url = (value: object) => Buffer.from(JSON.stringify(value)).toString('base64url');
/** An unsigned JWT: Amplify only decodes tokens in the browser, and every API call here is mocked. */
const jwt = (payload: object) => `${base64Url({ alg: 'none', typ: 'JWT' })}.${base64Url(payload)}.e2e`;

/** Seeds Amplify's token storage so the app starts signed in, with no Cognito round trip. */
export const signIn = async (page: Page) => {
  const exp = Math.floor(Date.now() / 1000) + 3600;
  const claims = { sub: 'e2e-sub', exp, iat: exp - 3600, iss: 'https://cognito-idp.us-east-1.amazonaws.com/us-east-1_E2E' };
  const prefix = `CognitoIdentityServiceProvider.${E2E_CLIENT_ID}`;
  const storage: Record<string, string> = {
    [`${prefix}.LastAuthUser`]: USERNAME,
    [`${prefix}.${USERNAME}.idToken`]: jwt({ ...claims, token_use: 'id', email: 'e2e@example.test', 'cognito:username': USERNAME }),
    [`${prefix}.${USERNAME}.accessToken`]: jwt({ ...claims, token_use: 'access', username: USERNAME }),
    [`${prefix}.${USERNAME}.refreshToken`]: 'e2e-refresh',
    [`${prefix}.${USERNAME}.clockDrift`]: '0',
  };
  await page.addInitScript((entries) => {
    for (const [key, value] of Object.entries(entries)) localStorage.setItem(key, value);
  }, storage);
};

const json = (route: Route, body: unknown) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

/** Answers every /api call from the fixtures. An unexpected call fails loudly so a new endpoint isn't silently empty. */
export const mockApi = async (page: Page) => {
  await page.route('**/api/**', (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname.replace(/^\/api/, '');
    const method = route.request().method();

    if (method === 'GET') {
      const responses: Record<string, unknown> = {
        '/users/me': data.user,
        '/data-version': { version: 1 },
        '/categories': data.categories,
        '/categories/usage': data.categoryUsage,
        '/category-rules': data.categoryRules,
        '/budget-plans': data.budgetPlans,
        '/money-map': data.moneyMap,
        '/budget-analysis': data.budgetAnalysis,
        '/cash-flow': data.cashFlow,
        '/transactions': data.transactions,
        '/transactions/page': data.transactionPage,
        '/transactions/summary': data.transactionSummary,
        '/plaid/connections': data.plaidConnections,
        '/webull': data.webull,
        '/webull/holdings': data.webullHoldings,
        '/webull/trades': data.webullTrades,
      };
      if (path in responses) return json(route, responses[path]);
    }
    if (method === 'POST' && path === '/plaid/sync') return json(route, { inserted: 0, updated: 0, removed: 0, syncedAt: new Date().toISOString() });
    if (method === 'POST' && path === '/category-rules/preview') return json(route, { count: 3 });
    if (method === 'PATCH' && path === '/transactions/category') {
      const { ids } = route.request().postDataJSON() as { ids: number[] };
      return json(route, { updated: ids.length });
    }
    if (method === 'PATCH' && path === '/transactions/notes') return route.fulfill({ status: 204 });

    return route.fulfill({ status: 501, contentType: 'application/json', body: JSON.stringify({ error: `e2e: no mock for ${method} ${path}` }) });
  });
};
