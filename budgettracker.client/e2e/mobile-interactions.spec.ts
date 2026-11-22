import { expect, test, type Locator, type Page } from '@playwright/test';
import { mockApi, signIn } from './support/session';

/** AC-3 (BUD-29): the dialog fits the screen, or scrolls inside it, and its main action can be reached. */
const expectFitsWithReachableAction = async (page: Page, dialog: Locator, action: Locator) => {
  await expect(dialog).toBeVisible();
  const box = (await dialog.boundingBox())!;
  const viewport = page.viewportSize()!;
  expect(box.x).toBeGreaterThanOrEqual(0);
  expect(box.x + box.width).toBeLessThanOrEqual(viewport.width);
  await action.scrollIntoViewIfNeeded();
  await expect(action).toBeInViewport();
};

const openList = async (page: Page) => {
  await signIn(page);
  await mockApi(page);
  await page.goto('/transactions');
  await page.getByRole('radio', { name: 'List' }).first().tap();
  await expect(page.getByText('WHOLE FOODS MARKET #10234 KATY TX').first()).toBeVisible();
};

test.describe('dialogs fit a phone', () => {
  test('rule dialog', async ({ page }) => {
    await openList(page);
    await page.getByRole('button', { name: 'Always categorize WHOLE FOODS MARKET #10234 KATY TX' }).tap();
    const dialog = page.getByRole('dialog', { name: 'Always categorize as' });
    await expectFitsWithReachableAction(page, dialog, dialog.getByRole('button', { name: 'Create rule' }));
  });

  test('disconnect dialog', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await page.goto('/settings');
    await page.getByRole('button', { name: /^Disconnect / }).first().tap();
    const dialog = page.getByRole('dialog', { name: /^Disconnect / });
    await expectFitsWithReachableAction(page, dialog, dialog.getByRole('button', { name: /^Disconnect/ }));
  });

  test('budget plan dialog', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await page.goto('/budget');
    await page.getByRole('button', { name: 'Edit', exact: true }).first().tap();
    const dialog = page.getByRole('dialog', { name: 'Edit budget plan' });
    await expectFitsWithReachableAction(page, dialog, dialog.getByRole('button', { name: 'Save changes' }));
  });
});

test.describe('transaction row works by touch (AC-5)', () => {
  test('category picker', async ({ page }) => {
    await openList(page);
    const request = page.waitForRequest((r) => r.method() === 'PATCH' && r.url().endsWith('/api/transactions/category'));
    await page.getByRole('combobox', { name: /^Category for UBER/ }).tap();
    await page.getByRole('option', { name: 'Rideshare' }).tap();
    expect((await request).postDataJSON()).toEqual({ ids: [1], categoryId: 3 });
  });

  test('suggestion chip', async ({ page }) => {
    await openList(page);
    const request = page.waitForRequest((r) => r.method() === 'PATCH' && r.url().endsWith('/api/transactions/category'));
    await page.getByRole('button', { name: 'Use Groceries' }).tap();
    expect((await request).postDataJSON()).toEqual({ ids: [5], categoryId: 1 });
  });

  test('note editor', async ({ page }) => {
    await openList(page);
    const request = page.waitForRequest((r) => r.method() === 'PATCH' && r.url().endsWith('/api/transactions/notes'));
    await page.getByRole('button', { name: 'Add note' }).first().tap();
    const note = page.getByRole('textbox', { name: /^Note for / });
    await note.fill('Split with Sam');
    await note.press('Enter');
    expect((await request).postDataJSON()).toEqual({ id: 1, notes: 'Split with Sam' });
  });
});
