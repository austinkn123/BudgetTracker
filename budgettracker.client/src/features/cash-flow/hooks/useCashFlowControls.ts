import { useEffect, useMemo, useState } from 'react';
import { parseISO, startOfDay, subDays } from 'date-fns';
import { z } from 'zod';
import type { CashFlowGrouping, CashFlowQuery } from '../../../shared/types/api';
import {
  compareWindow,
  defaultGrouping,
  rangeWindow,
  toDateKey,
  type CashFlowRangeKey,
  type DateSpan,
} from '../utils/cashFlow';

const STORAGE_KEY = 'bt:cash-flow:controls';
const SEARCH_DEBOUNCE_MS = 300;
const RANGES = ['month', '3m', 'ytd', '12m', 'all', 'custom'] as const satisfies readonly CashFlowRangeKey[];
const GROUPINGS = ['Day', 'Week', 'Month', 'Year'] as const satisfies readonly CashFlowGrouping[];

const today = () => startOfDay(new Date());

/** Each field falls back on its own, so one bad value in storage doesn't reset the rest. */
const controlsSchema = z.object({
  range: z.enum(RANGES).catch('month'),
  customFrom: z.iso.date().catch(() => toDateKey(subDays(today(), 29))),
  customTo: z.iso.date().catch(() => toDateKey(today())),
  /** A manual grouping; null follows the range's default. */
  grouping: z.enum(GROUPINGS).nullable().catch(null),
  accountId: z.string().nullable().catch(null),
  categoryId: z.number().int().nullable().catch(null),
  search: z.string().catch(''),
});

type Controls = z.infer<typeof controlsSchema>;

const readControls = (): Controls => {
  try {
    return controlsSchema.parse(JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? '{}'));
  } catch {
    // Storage blocked (private mode) or not an object: start from defaults.
    return controlsSchema.parse({});
  }
};

/**
 * Every Cash Flow control, persisted to localStorage so the page reopens as it was left, and the
 * API query they add up to. Search reaches the query 300ms after typing stops.
 */
export const useCashFlowControls = () => {
  const [controls, setControls] = useState(readControls);
  const [debouncedSearch, setDebouncedSearch] = useState(controls.search);

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(controls));
    } catch {
      // Ignore quota / private-mode errors.
    }
  }, [controls]);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(controls.search), SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [controls.search]);

  const update = (patch: Partial<Controls>) => setControls((current) => ({ ...current, ...patch }));

  const { range, customFrom, customTo, accountId, categoryId } = controls;

  const bounds = useMemo(
    () => rangeWindow(range, today(), { from: parseISO(customFrom), to: parseISO(customTo) }),
    [range, customFrom, customTo],
  );

  const grouping = controls.grouping ?? defaultGrouping(range, bounds);

  const query = useMemo<CashFlowQuery>(() => {
    const compare = compareWindow(range, bounds);
    const search = debouncedSearch.trim();
    return {
      from: bounds.from ? toDateKey(bounds.from) : undefined,
      to: toDateKey(bounds.to),
      groupBy: grouping,
      compareFrom: compare ? toDateKey(compare.from) : undefined,
      compareTo: compare ? toDateKey(compare.to) : undefined,
      plaidAccountId: accountId ?? undefined,
      categoryId: categoryId ?? undefined,
      search: search || undefined,
    };
  }, [range, bounds, grouping, accountId, categoryId, debouncedSearch]);

  return {
    range,
    bounds,
    grouping,
    accountId,
    categoryId,
    search: controls.search,
    query,
    // A new range (or new custom dates) drops any manual grouping: the default fits the new span.
    setRange: (next: CashFlowRangeKey) => update({ range: next, grouping: null }),
    setCustomRange: (span: DateSpan) =>
      update({
        range: 'custom',
        customFrom: toDateKey(span.from),
        customTo: toDateKey(span.to),
        grouping: null,
      }),
    setGrouping: (next: CashFlowGrouping) => update({ grouping: next }),
    setAccountId: (next: string | null) => update({ accountId: next }),
    setCategoryId: (next: number | null) => update({ categoryId: next }),
    setSearch: (next: string) => update({ search: next }),
  };
};

export type CashFlowControls = ReturnType<typeof useCashFlowControls>;
