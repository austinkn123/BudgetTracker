import { beforeEach, describe, expect, it } from 'vitest';
import type { BudgetPlan, BudgetPlanEntry, Category, MoneyMap, MoneyMapLine } from '../../../shared/types/api';
import {
  addableCategories,
  addItem,
  buildDraft,
  clearSavedDraft,
  newItemKey,
  pickPlan,
  planStamp,
  readSavedDraft,
  toPlan,
  totals,
  writeSavedDraft,
} from './budgetBuilder';

const category = (id: number, name: string, categoryType = 'Expense'): Category => ({
  id,
  userId: 3,
  name,
  categoryType,
});

const CATEGORIES = [
  category(1, 'Mortgage'),
  category(2, 'Groceries'),
  category(3, 'Car registration'),
  category(4, 'Eating out'),
  category(5, 'Coffee'),
  category(6, 'Paycheck', 'Income'),
  category(7, 'Gifts'),
];

const entry = (over: Partial<BudgetPlanEntry>): BudgetPlanEntry => ({
  id: 1,
  budgetPlanId: 10,
  categoryId: 1,
  lineType: 'Expense',
  bucket: 'Core',
  cadence: 'Monthly',
  amount: 100,
  monthlyEquivalent: 100,
  isStressFactor: false,
  notes: null,
  sortOrder: 0,
  createdAt: '2026-09-01T00:00:00Z',
  ...over,
});

const plan = (over: Partial<BudgetPlan> = {}): BudgetPlan => ({
  id: 10,
  userId: 3,
  name: 'Fall plan',
  planMonth: '2026-09-01',
  netIncomeMonthly: 5646,
  isActive: true,
  createdAt: '2026-09-01T00:00:00Z',
  entries: [
    entry({ id: 1, categoryId: 6, lineType: 'Income', amount: 5646, sortOrder: 0 }),
    entry({ id: 2, categoryId: 1, bucket: 'Core', amount: 3200, sortOrder: 1, notes: 'Fixed rate' }),
    entry({ id: 3, categoryId: 2, bucket: 'Buffer', amount: 600, sortOrder: 2, isStressFactor: true }),
    entry({ id: 4, categoryId: 3, bucket: 'Buffer', cadence: 'Annual', amount: 240, monthlyEquivalent: 20, sortOrder: 3 }),
  ],
  ...over,
});

const line = (categoryId: number | null, actualMonthly: number, bucket: MoneyMapLine['bucket'] = null): MoneyMapLine => ({
  categoryId,
  name: '',
  bucket,
  cadence: null,
  planMonthly: 0,
  actualMonthly,
  topPayees: [],
  notes: null,
});

const map = (lines: MoneyMapLine[], actualIncomeMonthly = 5700): MoneyMap => ({
  planName: 'Fall plan',
  from: '2025-12-01',
  to: '2026-11-03',
  months: 12,
  plannedIncomeMonthly: 5646,
  actualIncomeMonthly,
  lines,
  upcoming: [],
});

const MAP = map([
  line(1, 3200, 'Core'),
  line(2, 612.4, 'Buffer'),
  line(3, 19, 'Buffer'),
  line(4, 214, null),
  line(5, 12, null), // too small to suggest
  line(null, 300, null), // uncategorized
]);

describe('pickPlan', () => {
  it('takes the newest active plan, like the money map does', () => {
    const plans = [
      plan({ id: 1, planMonth: '2026-01-01' }),
      plan({ id: 2, planMonth: '2026-09-01' }),
      plan({ id: 3, planMonth: '2026-10-01', isActive: false }),
    ];
    expect(pickPlan(plans)?.id).toBe(2);
  });

  it('falls back to the newest plan when none is active, and to null when there are none', () => {
    const plans = [plan({ id: 1, planMonth: '2026-01-01', isActive: false }), plan({ id: 2, planMonth: '2026-03-01', isActive: false })];
    expect(pickPlan(plans)?.id).toBe(2);
    expect(pickPlan([])).toBeNull();
  });
});

describe('buildDraft', () => {
  const draft = buildDraft(plan(), MAP, CATEGORIES);

  it('sorts expense lines into steps: must-pay monthly, flexible monthly, once a year', () => {
    expect(draft.bills.map((i) => i.name)).toEqual(['Mortgage']);
    expect(draft.everyday.filter((i) => i.on).map((i) => i.name)).toEqual(['Groceries']);
    expect(draft.yearly.map((i) => [i.name, i.amount])).toEqual([['Car registration', 240]]);
  });

  it('keeps the plan income and attaches the bank average to each line', () => {
    expect(draft.income).toBe(5646);
    expect(draft.everyday.find((i) => i.name === 'Groceries')?.bankMonthly).toBe(612.4);
  });

  it('offers unplanned spending as unticked everyday items, rounded to $10, skipping small and uncategorized spend', () => {
    const offered = draft.everyday.filter((i) => !i.on);
    expect(offered.map((i) => [i.name, i.amount])).toEqual([['Eating out', 210]]);
  });

  it('starts from the bank when there is no plan', () => {
    const fresh = buildDraft(null, map([line(4, 214)], 4748), CATEGORIES);
    expect(fresh.income).toBe(4750);
    expect(fresh.bills).toEqual([]);
    expect(fresh.everyday.map((i) => [i.name, i.on])).toEqual([['Eating out', false]]);
  });
});

describe('totals', () => {
  it('counts ticked items only, with yearly bills as a monthly share', () => {
    const draft = buildDraft(plan(), MAP, CATEGORIES);
    // 5646 - 3200 - 600 - 240/12; Eating out is unticked.
    expect(totals(draft)).toEqual({ bills: 3200, everyday: 600, yearly: 20, left: 1826 });
  });
});

describe('addableCategories and addItem', () => {
  it('lists expense categories not in this step or ticked in another, most spent first', () => {
    const draft = buildDraft(plan(), MAP, CATEGORIES);
    // Groceries and Car registration are ticked elsewhere, so adding them here would count them twice.
    expect(addableCategories(draft, 'bills', CATEGORIES, MAP).map((c) => c.category.name)).toEqual([
      'Eating out',
      'Coffee',
      'Gifts',
    ]);
  });

  it('adds a ticked item at the bank amount; a yearly item gets twelve months of it', () => {
    const draft = buildDraft(plan(), MAP, CATEGORIES);
    const withCoffee = addItem(draft, 'everyday', category(5, 'Coffee'), 12);
    expect(withCoffee.everyday.at(-1)).toMatchObject({ key: newItemKey('everyday', 5), name: 'Coffee', amount: 10, on: true });
    const withGifts = addItem(draft, 'yearly', category(7, 'Gifts'), 41);
    expect(withGifts.yearly.at(-1)).toMatchObject({ name: 'Gifts', amount: 490, on: true });
  });
});

describe('saved drafts', () => {
  beforeEach(() => window.localStorage.clear());

  it('stamps a plan by id and last change, so an edit elsewhere tells them apart', () => {
    expect(planStamp(plan({ updatedAt: '2026-10-02T00:00:00Z' }))).toBe('10:2026-10-02T00:00:00Z');
    expect(planStamp(plan({ updatedAt: null }))).toBe('10:2026-09-01T00:00:00Z');
    expect(planStamp(null)).toBe('new');
  });

  it('gives back the screen and answers saved for the same plan', () => {
    const draft = addItem(buildDraft(plan(), MAP, CATEGORIES), 'bills', category(5, 'Coffee'), 12);
    writeSavedDraft('10:a', 'everyday', draft);

    expect(readSavedDraft('10:a')).toEqual({ screen: 'everyday', draft });
  });

  it('ignores a draft for a plan that has changed since, a cleared one, or one that is not valid', () => {
    writeSavedDraft('10:a', 'bills', buildDraft(plan(), MAP, CATEGORIES));
    expect(readSavedDraft('10:b')).toBeNull();

    clearSavedDraft();
    expect(readSavedDraft('10:a')).toBeNull();

    window.localStorage.setItem('bt:budget-builder:draft', '{"stamp":"10:a","screen":"bills","draft":{"income":"lots"}}');
    expect(readSavedDraft('10:a')).toBeNull();
    window.localStorage.setItem('bt:budget-builder:draft', 'not json');
    expect(readSavedDraft('10:a')).toBeNull();
  });
});

describe('toPlan', () => {
  const NOW = new Date('2026-11-03T12:00:00Z');

  it('updates the plan in place: keeps income lines and untouched fields, drops unticked lines, adds new ones', () => {
    const base = plan();
    let draft = buildDraft(base, MAP, CATEGORIES);
    draft = { ...draft, income: 5800 };
    draft = { ...draft, bills: draft.bills.map((i) => ({ ...i, amount: 3250 })) };
    draft = { ...draft, yearly: draft.yearly.map((i) => ({ ...i, on: false })) };
    draft = addItem(draft, 'everyday', category(4, 'Eating out'), 214); // ticks the offered item

    const saved = toPlan(draft, base, NOW);

    expect(saved).toMatchObject({ id: 10, name: 'Fall plan', planMonth: '2026-09-01', netIncomeMonthly: 5800, isActive: true });
    expect(saved.entries.map((e) => [e.id, e.categoryId, e.bucket, e.cadence, e.amount])).toEqual([
      [1, 6, 'Core', 'Monthly', 5646],
      [2, 1, 'Core', 'Monthly', 3250],
      [3, 2, 'Buffer', 'Monthly', 600],
      [0, 4, 'Buffer', 'Monthly', 210],
    ]);
    expect(saved.entries[1].notes).toBe('Fixed rate');
    expect(saved.entries[2].isStressFactor).toBe(true);
    expect(saved.entries.map((e) => e.sortOrder)).toEqual([0, 1, 2, 3]);
  });

  it("keeps the plan's line order, with new lines last", () => {
    const base = plan({
      entries: [
        entry({ id: 1, categoryId: 3, cadence: 'Annual', amount: 240, monthlyEquivalent: 20, sortOrder: 0 }),
        entry({ id: 2, categoryId: 2, bucket: 'Buffer', amount: 600, sortOrder: 1 }),
        entry({ id: 3, categoryId: 1, bucket: 'Core', amount: 3200, sortOrder: 2 }),
      ],
    });
    const draft = addItem(buildDraft(base, MAP, CATEGORIES), 'bills', category(5, 'Coffee'), 12);

    expect(toPlan(draft, base, NOW).entries.map((e) => [e.id, e.sortOrder])).toEqual([
      [1, 0],
      [2, 1],
      [3, 2],
      [0, 3],
    ]);
  });

  it('makes a new active plan for this month when there is none', () => {
    let draft = buildDraft(null, map([]), CATEGORIES);
    draft = addItem(draft, 'yearly', category(3, 'Car registration'), 20);

    const saved = toPlan(draft, null, NOW);

    expect(saved).toMatchObject({ name: 'My budget', planMonth: '2026-11-01', isActive: true });
    expect(saved.entries).toMatchObject([
      { id: 0, categoryId: 3, lineType: 'Expense', bucket: 'Core', cadence: 'Annual', amount: 240, monthlyEquivalent: 20 },
    ]);
  });
});
