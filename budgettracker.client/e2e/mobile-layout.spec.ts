import { writeFileSync, mkdirSync } from 'node:fs';
import { expect, test, type Page, type TestInfo } from '@playwright/test';
import { mockApi, signIn } from './support/session';

/** Every signed-in page (BUD-29). `ready` is text that only shows once the page has its data. */
const PAGES = [
  { path: '/', name: 'Money Map', ready: 'Household plan' },
  { path: '/budget', name: 'Budget Builder', ready: 'Change your monthly budget' },
  { path: '/dashboard', name: 'Dashboard', ready: 'Groceries' },
  { path: '/cash-flow', name: 'Cash Flow', ready: 'ACME CORP PAYROLL DIRECT DEP' },
  { path: '/transactions', name: 'Transactions', ready: 'Groceries' },
  { path: '/investments', name: 'Investments', ready: 'Holdings' },
  { path: '/settings', name: 'Settings', ready: 'Rules' },
] as const;

/** Option rows that scroll sideways must not also scroll vertically (an overhanging hit area once made them wobble). */
const verticallyScrollingRows = (page: Page) =>
  page.evaluate(() =>
    Array.from(document.querySelectorAll<HTMLElement>('[role="radiogroup"], [role="group"], [role="tablist"]'))
      .filter((el) => ['auto', 'scroll'].includes(getComputedStyle(el).overflowY) && el.scrollHeight > el.clientHeight)
      .map((el) => `${el.getAttribute('aria-label') ?? el.getAttribute('role')} ${el.scrollHeight}>${el.clientHeight}`),
  );

const horizontalOverflow = (page: Page) =>
  page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);

/** Visible controls smaller than 44×44, for the audit. */
const smallTapTargets = (page: Page) =>
  page.evaluate(() =>
    Array.from(document.querySelectorAll<HTMLElement>('button, a[href], input, select, [role="button"], [role="combobox"], [role="tab"], [role="checkbox"], [role="radio"]'))
      .map((el) => {
        const r = el.getBoundingClientRect();
        // touch-target gives a 44×44 hit area on touch screens without changing the visible box.
        const min = el.classList.contains('touch-target') ? 44 : 0;
        return { el, r: { width: Math.max(r.width, min), height: Math.max(r.height, min), visible: r.width > 0 && r.height > 0 } };
      })
      .filter(({ el }) => !el.closest('.tsqd-parent-container') && el.getAttribute('aria-label') !== 'Open Tanstack query devtools')
      .filter(({ r }) => r.visible && (r.width < 44 || r.height < 44))
      .map(({ el, r }) => `${el.getAttribute('aria-label') || el.textContent?.trim().slice(0, 30) || el.tagName} ${Math.round(r.width)}x${Math.round(r.height)}`),
  );

/** Screenshot and tap-target list for the audit; also written to E2E_AUDIT_DIR when set, for review outside the report. */
const record = async (page: Page, testInfo: TestInfo, label: string) => {
  // Measure first: in Chromium a full-page screenshot resets the touch emulation, so (pointer: coarse) stops
  // matching for the rest of the test and touch-target sizes would read as their visual size.
  const small = await smallTapTargets(page);
  const taps = small.join('\n');
  const shot = await page.screenshot({ fullPage: true });
  await testInfo.attach(`${label}.png`, { body: shot, contentType: 'image/png' });
  await testInfo.attach(`${label}-small-tap-targets.txt`, { body: taps, contentType: 'text/plain' });
  const dir = process.env.E2E_AUDIT_DIR;
  if (dir) {
    mkdirSync(dir, { recursive: true });
    const name = `${testInfo.project.name}-${label}`.replace(/[^\w-]+/g, '_');
    writeFileSync(`${dir}/${name}.png`, shot);
    writeFileSync(`${dir}/${name}.txt`, taps);
  }
  return small;
};

for (const target of PAGES) {
  test(`${target.name} fits the screen`, async ({ page }, testInfo) => {
    await signIn(page);
    await mockApi(page);
    await page.goto(target.path);
    await expect(page.getByText(target.ready).first()).toBeVisible();
    await expect(page.getByRole('status', { name: 'Loading page' })).toHaveCount(0);

    const overflow = await horizontalOverflow(page);
    const wobbling = await verticallyScrollingRows(page);
    const small = await record(page, testInfo, target.name);

    expect(overflow).toBeLessThanOrEqual(0);
    expect(wobbling).toEqual([]);
    expect(small).toEqual([]);
  });
}

test('Money Map Plan tab fits the screen', async ({ page }, testInfo) => {
  await signIn(page);
  await mockApi(page);
  await page.goto('/');
  await page.getByRole('tab', { name: 'Plan' }).or(page.getByRole('radio', { name: 'Plan' })).first().click();
  await expect(page.getByRole('button', { name: 'Edit', exact: true }).first()).toBeVisible();

  const overflow = await horizontalOverflow(page);
  const small = await record(page, testInfo, 'Money-Map-Plan');
  expect(overflow).toBeLessThanOrEqual(0);
  expect(small).toEqual([]);
});

for (const view of ['List', 'Calendar'] as const) {
  test(`Transactions ${view} view fits the screen`, async ({ page }, testInfo) => {
    await signIn(page);
    await mockApi(page);
    await page.goto('/transactions');
    await page.getByRole('radio', { name: view }).or(page.getByRole('tab', { name: view })).first().click();
    await expect(view === 'List' ? page.getByText('WHOLE FOODS MARKET #10234 KATY TX').first() : page.getByRole('grid').first()).toBeVisible();
    const overflow = await horizontalOverflow(page);
    const small = await record(page, testInfo, `Transactions-${view}`);

    expect(overflow).toBeLessThanOrEqual(0);
    expect(small).toEqual([]);
  });
}

test('Login fits the screen', async ({ page }, testInfo) => {
  await mockApi(page);
  await page.goto('/login');
  await expect(page.getByRole('button', { name: /sign in/i })).toBeVisible();
  const overflow = await horizontalOverflow(page);
  const small = await record(page, testInfo, 'Login');
  expect(overflow).toBeLessThanOrEqual(0);
  expect(small).toEqual([]);
});
