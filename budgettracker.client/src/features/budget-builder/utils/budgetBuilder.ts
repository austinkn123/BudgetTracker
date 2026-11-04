import { format } from 'date-fns';
import { z } from 'zod';
import type { BudgetPlan, BudgetPlanEntry, Category, MoneyMap } from '../../../shared/types/api';

/**
 * The step-by-step budget builder's draft: the active plan's expense lines sorted into three plain-language steps, each
 * with the bank's monthly average beside it. Nothing is saved until toPlan turns the draft back into the plan.
 */

export type StepKey = 'bills' | 'everyday' | 'yearly';

export const ITEM_STEPS: readonly StepKey[] = ['bills', 'everyday', 'yearly'];

export interface Item {
  key: string;
  categoryId: number | null;
  name: string;
  /** Yearly for the once-a-year step, monthly otherwise. */
  amount: number;
  /** Ticked items are in the budget; unticked ones stay listed so they can be ticked back. */
  on: boolean;
  /** The bank's monthly average for this category; 0 when there is none. */
  bankMonthly: number;
  /** The plan line this came from. New items have none. */
  entry?: BudgetPlanEntry;
}

export interface Draft {
  income: number;
  bills: Item[];
  everyday: Item[];
  yearly: Item[];
}

export interface Totals {
  bills: number;
  everyday: number;
  yearly: number;
  left: number;
}

/** Unplanned spending below this a month is not worth offering; it can still be added by hand. */
const MIN_SUGGESTION = 25;

const NEW_PLAN_NAME = 'My budget';

export const roundTo10 = (amount: number) => Math.round(amount / 10) * 10;

export const isExpenseCategory = (category: Category) =>
  category.categoryType === 'Expense' || category.categoryType === 'Both';

/** A yearly bill counts as a twelfth of itself each month. */
export const monthlyOf = (item: Item, step: StepKey) => (step === 'yearly' ? item.amount / 12 : item.amount);

/** What the bank suggests for a new item: its monthly average, or a year of it for the once-a-year step. */
const suggest = (bankMonthly: number, step: StepKey) => roundTo10(step === 'yearly' ? bankMonthly * 12 : bankMonthly);

const stepOf = (entry: BudgetPlanEntry): StepKey =>
  entry.cadence === 'Annual' ? 'yearly' : entry.bucket === 'Core' ? 'bills' : 'everyday';

const bankByCategory = (map: MoneyMap) =>
  new Map(map.lines.flatMap((l) => (l.categoryId === null ? [] : [[l.categoryId, l.actualMonthly] as const])));

/** The plan the builder edits: the newest active one, as the money map measures against, else the newest of any. */
export const pickPlan = (plans: BudgetPlan[]) => {
  const newest = [...plans].sort((a, b) => b.planMonth.localeCompare(a.planMonth));
  return newest.find((p) => p.isActive) ?? newest[0] ?? null;
};

export const buildDraft = (plan: BudgetPlan | null, map: MoneyMap, categories: Category[]): Draft => {
  const names = new Map(categories.map((c) => [c.id, c.name]));
  const bank = bankByCategory(map);
  const draft: Draft = { income: plan ? plan.netIncomeMonthly : roundTo10(map.actualIncomeMonthly), bills: [], everyday: [], yearly: [] };

  const expenses = (plan?.entries ?? []).filter((e) => e.lineType === 'Expense').sort((a, b) => a.sortOrder - b.sortOrder);
  for (const entry of expenses) {
    draft[stepOf(entry)].push({
      key: `entry-${entry.id}`,
      categoryId: entry.categoryId ?? null,
      name: entry.categoryId ? (names.get(entry.categoryId) ?? 'Other') : 'Other',
      amount: entry.amount,
      on: true,
      bankMonthly: entry.categoryId ? (bank.get(entry.categoryId) ?? 0) : 0,
      entry,
    });
  }

  // Spending the plan doesn't cover yet, biggest first (the server's order), offered unticked.
  const planned = new Set(expenses.map((e) => e.categoryId));
  const expenseIds = new Set(categories.filter(isExpenseCategory).map((c) => c.id));
  for (const line of map.lines) {
    if (line.categoryId === null || planned.has(line.categoryId) || !expenseIds.has(line.categoryId)) continue;
    if (line.actualMonthly < MIN_SUGGESTION) continue;
    draft.everyday.push({
      key: `category-${line.categoryId}`,
      categoryId: line.categoryId,
      name: names.get(line.categoryId) ?? line.name,
      amount: suggest(line.actualMonthly, 'everyday'),
      on: false,
      bankMonthly: line.actualMonthly,
    });
  }

  return draft;
};

export const totals = (draft: Draft): Totals => {
  const sum = (step: StepKey) => draft[step].filter((i) => i.on).reduce((total, i) => total + monthlyOf(i, step), 0);
  const [bills, everyday, yearly] = ITEM_STEPS.map(sum);
  return { bills, everyday, yearly, left: draft.income - bills - everyday - yearly };
};

/**
 * Expense categories that can still be added to a step, most spent first. A category ticked in another step is left out,
 * since budgeting it twice would count its spending twice.
 */
export const addableCategories = (draft: Draft, step: StepKey, categories: Category[], map: MoneyMap) => {
  const taken = new Set([
    ...draft[step].map((i) => i.categoryId),
    ...ITEM_STEPS.flatMap((s) => draft[s].filter((i) => i.on).map((i) => i.categoryId)),
  ]);
  const bank = bankByCategory(map);
  return categories
    .filter((c) => isExpenseCategory(c) && !taken.has(c.id))
    .map((category) => ({ category, bankMonthly: bank.get(category.id) ?? 0 }))
    .sort((a, b) => b.bankMonthly - a.bankMonthly || a.category.name.localeCompare(b.category.name));
};

/** The key addItem gives a category it adds to a step, so the caller can open it straight away. */
export const newItemKey = (step: StepKey, categoryId: number) => `new-${step}-${categoryId}`;

/** Ticks the category's item in the step, adding it at the bank's figure if it isn't listed yet. */
export const addItem = (draft: Draft, step: StepKey, category: Category, bankMonthly: number): Draft => {
  const existing = draft[step].find((i) => i.categoryId === category.id);
  const items = existing
    ? draft[step].map((i) => (i === existing ? { ...i, on: true } : i))
    : [
        ...draft[step],
        {
          key: newItemKey(step, category.id),
          categoryId: category.id,
          name: category.name,
          amount: suggest(bankMonthly, step),
          on: true,
          bankMonthly,
        },
      ];
  return { ...draft, [step]: items };
};

// ---- Saved drafts: the answers so far, kept in localStorage so a reload or a closed tab doesn't lose them ----

const DRAFT_KEY = 'bt:budget-builder:draft';

/** The screens a saved draft can reopen on, in the order the builder shows them. */
export const RESUMABLE_SCREENS = ['income', 'bills', 'everyday', 'yearly', 'review'] as const;
export type ResumableScreen = (typeof RESUMABLE_SCREENS)[number];

const itemSchema = z.object({
  key: z.string(),
  categoryId: z.number().nullable(),
  name: z.string(),
  amount: z.number(),
  on: z.boolean(),
  bankMonthly: z.number(),
  // Copied from the plan as the server sent it; the stamp ties the draft to that version of the plan.
  entry: z.custom<BudgetPlanEntry>((value) => typeof value === 'object' && value !== null).optional(),
});

const savedDraftSchema = z.object({
  stamp: z.string(),
  screen: z.enum(RESUMABLE_SCREENS),
  draft: z.object({ income: z.number(), bills: z.array(itemSchema), everyday: z.array(itemSchema), yearly: z.array(itemSchema) }),
});

/** Which version of which plan a draft was built from. A plan saved since, here or on the Plan tab, gets a new stamp. */
export const planStamp = (plan: BudgetPlan | null) => (plan ? `${plan.id}:${plan.updatedAt ?? plan.createdAt}` : 'new');

/** The saved screen and answers, or null when there are none for this version of the plan. */
export const readSavedDraft = (stamp: string): { screen: ResumableScreen; draft: Draft } | null => {
  try {
    const saved = savedDraftSchema.safeParse(JSON.parse(window.localStorage.getItem(DRAFT_KEY) ?? 'null'));
    return saved.success && saved.data.stamp === stamp ? { screen: saved.data.screen, draft: saved.data.draft } : null;
  } catch {
    // Storage blocked (private mode) or not JSON: nothing to resume.
    return null;
  }
};

export const writeSavedDraft = (stamp: string, screen: ResumableScreen, draft: Draft) => {
  try {
    window.localStorage.setItem(DRAFT_KEY, JSON.stringify({ stamp, screen, draft }));
  } catch {
    // Ignore quota / private-mode errors; the builder still works, it just won't survive a reload.
  }
};

export const clearSavedDraft = () => {
  try {
    window.localStorage.removeItem(DRAFT_KEY);
  } catch {
    // Same as above.
  }
};

/** Yearly bills (insurance, registration, HOA) are must-pay, like the monthly bills. */
const BUCKET: Record<StepKey, BudgetPlanEntry['bucket']> = { bills: 'Core', everyday: 'Buffer', yearly: 'Core' };

/** Existing lines keep the plan's order; new ones go last, in step order. */
const orderOf = (entry: BudgetPlanEntry) => (entry.id > 0 ? entry.sortOrder : Number.MAX_SAFE_INTEGER);

/**
 * The plan to save. Income lines, line order, and every field the builder doesn't show (notes, stress factor) carry
 * over untouched; an existing line left unticked is dropped, which the server treats as a delete. With no plan, a new
 * active one starts this month.
 */
export const toPlan = (draft: Draft, plan: BudgetPlan | null, now: Date): BudgetPlan => {
  const createdAt = now.toISOString();
  const income = (plan?.entries ?? []).filter((e) => e.lineType === 'Income');
  const expenses = ITEM_STEPS.flatMap((step) =>
    draft[step]
      .filter((i) => i.on)
      .map(
        (i): BudgetPlanEntry =>
          i.entry
            ? { ...i.entry, amount: i.amount }
            : {
                id: 0,
                budgetPlanId: plan?.id ?? 0,
                categoryId: i.categoryId,
                lineType: 'Expense',
                bucket: BUCKET[step],
                cadence: step === 'yearly' ? 'Annual' : 'Monthly',
                amount: i.amount,
                monthlyEquivalent: monthlyOf(i, step),
                isStressFactor: false,
                notes: null,
                sortOrder: 0,
                createdAt,
              },
      ),
  );
  const entries = [...income, ...expenses]
    .sort((a, b) => orderOf(a) - orderOf(b))
    .map((e, sortOrder) => ({ ...e, sortOrder }));

  return plan
    ? { ...plan, netIncomeMonthly: draft.income, isActive: true, updatedAt: createdAt, entries }
    : {
        id: 0,
        userId: 0,
        name: NEW_PLAN_NAME,
        planMonth: format(now, 'yyyy-MM-01'),
        netIncomeMonthly: draft.income,
        isActive: true,
        createdAt,
        entries,
      };
};
