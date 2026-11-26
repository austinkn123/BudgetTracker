import { expect, test, type Page } from '@playwright/test';
import type { BudgetPlan, Category, MoneyMap } from '../src/shared/types/api';
import { mockApi, signIn } from './support/session';

/**
 * A brand-new user right after their first bank sync: starter categories exist (one per Plaid category), there is no
 * budget yet, and some spending (Venmo, Zelle) is unsorted. The guide should arrive with a full draft to save.
 */
const starter = (id: number, name: string, primary: string, categoryType = 'Expense'): Category => ({
  id,
  userId: 9,
  name,
  categoryType,
  plaidCategoryPrimary: primary,
});

const categories: Category[] = [
  starter(11, 'Income', 'INCOME', 'Income'),
  starter(12, 'Loan payments', 'LOAN_PAYMENTS'),
  starter(13, 'Housing & utilities', 'RENT_AND_UTILITIES'),
  starter(14, 'Food & dining', 'FOOD_AND_DRINK'),
  starter(15, 'Shopping', 'GENERAL_MERCHANDISE'),
];

const line = (categoryId: number | null, name: string, actualMonthly: number) => ({
  categoryId,
  name,
  bucket: null,
  cadence: null,
  planMonthly: 0,
  actualMonthly,
  topPayees: [],
  notes: null,
});

const moneyMap: MoneyMap = {
  planName: null,
  from: '2025-12-01',
  to: '2026-11-26',
  months: 12,
  plannedIncomeMonthly: 0,
  actualIncomeMonthly: 4812,
  lines: [
    line(12, 'Loan payments', 1850),
    line(14, 'Food & dining', 392.5),
    line(13, 'Housing & utilities', 140),
    line(null, 'Uncategorized', 135),
    line(15, 'Shopping', 45),
  ],
  upcoming: [],
};

const openGuideAsNewUser = async (page: Page) => {
  await signIn(page);
  await mockApi(page);
  const saved: BudgetPlan[] = [];
  // Registered after mockApi, so these answer first.
  await page.route('**/api/categories', (route) => route.fulfill({ json: categories }));
  await page.route('**/api/money-map**', (route) => route.fulfill({ json: moneyMap }));
  await page.route('**/api/budget-plans', (route) => {
    if (route.request().method() === 'POST') {
      const plan = route.request().postDataJSON() as BudgetPlan;
      saved.push(plan);
      return route.fulfill({ status: 201, json: { ...plan, id: 77 } });
    }
    return route.fulfill({ json: saved.length ? [{ ...saved[0], id: 77 }] : [] });
  });
  await page.goto('/budget/guide');
  return saved;
};

const ticked = (page: Page, name: string) => page.getByRole('checkbox', { name: new RegExp(name) });

test('a new user gets a full first budget drafted from their bank', async ({ page }) => {
  const saved = await openGuideAsNewUser(page);

  await expect(page.getByRole('heading', { name: 'Make your monthly budget' })).toBeVisible();
  await page.getByRole('button', { name: 'Start', exact: true }).click();

  // Income comes from the bank's 12-month average, rounded to $10.
  await expect(page.getByRole('spinbutton', { name: 'Each month' })).toHaveValue('4810');
  await page.getByRole('button', { name: 'Next' }).click();

  // Bills: housing and loan payments, already ticked.
  await expect(ticked(page, 'Loan payments')).toHaveAttribute('aria-checked', 'true');
  await expect(ticked(page, 'Housing & utilities')).toHaveAttribute('aria-checked', 'true');
  await page.getByRole('button', { name: 'Next' }).click();

  // Everyday: the rest, ticked, plus unsorted spending so the total isn't short.
  await expect(ticked(page, 'Food & dining')).toHaveAttribute('aria-checked', 'true');
  await expect(ticked(page, 'Shopping')).toHaveAttribute('aria-checked', 'true');
  await expect(ticked(page, 'Other / not sorted yet')).toHaveAttribute('aria-checked', 'true');
  // $135 of $2,562.50 a month is unsorted.
  await expect(page.getByText('95% of your spending is sorted.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Sort them in Transactions' })).toHaveAttribute('href', '/transactions');
  await page.getByRole('button', { name: 'Next' }).click();

  // Yearly: nothing drafted (not detected yet), then review and save.
  await page.getByRole('button', { name: 'Next' }).click();
  await page.getByRole('button', { name: 'Save my budget' }).click();

  await expect.poll(() => saved.length).toBe(1);
  const plan = saved[0];
  expect(plan.isActive).toBe(true);
  expect(plan.netIncomeMonthly).toBe(4810);
  const lines = plan.entries.map((e) => [e.categoryId, e.bucket, e.amount]);
  expect(lines).toEqual(
    expect.arrayContaining([
      [12, 'Core', 1850],
      [13, 'Core', 140],
      [14, 'Buffer', 390],
      [15, 'Buffer', 50],
      [null, 'Buffer', 140],
    ]),
  );
  expect(lines).toHaveLength(5);
});
