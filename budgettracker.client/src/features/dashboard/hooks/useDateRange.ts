import { useCallback, useEffect, useMemo, useState } from 'react';
import { startOfMonth, endOfMonth, subMonths, startOfYear } from 'date-fns';

export type DateRangeKey = 'month' | '3m' | 'ytd' | 'all';

const STORAGE_KEY = 'bt:dashboard:range';
const VALID_RANGES: DateRangeKey[] = ['month', '3m', 'ytd', 'all'];

function readPersistedRange(): DateRangeKey {
  if (typeof window === 'undefined') return 'month';
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (raw && (VALID_RANGES as string[]).includes(raw)) {
      return raw as DateRangeKey;
    }
  } catch {
    // Access can throw in private mode; fall through to default.
  }
  return 'month';
}

function persistRange(range: DateRangeKey): void {
  if (typeof window === 'undefined') return;
  try {
    window.sessionStorage.setItem(STORAGE_KEY, range);
  } catch {
    // Ignore quota / private-mode errors.
  }
}

/**
 * Derive an inclusive [start, end) date window for a given range key.
 * `end` is the upper-bound timestamp; for `'all'` we use a far-future date.
 */
function computeBounds(range: DateRangeKey, now: Date): { start: Date; end: Date } {
  switch (range) {
    case 'month':
      return { start: startOfMonth(now), end: endOfMonth(now) };
    case '3m':
      return { start: startOfMonth(subMonths(now, 2)), end: endOfMonth(now) };
    case 'ytd':
      return { start: startOfYear(now), end: endOfMonth(now) };
    case 'all':
    default:
      return { start: new Date(1970, 0, 1), end: new Date(9999, 11, 31) };
  }
}

export interface UseDateRangeResult {
  range: DateRangeKey;
  setRange: (next: DateRangeKey) => void;
  start: Date;
  end: Date;
}

/**
 * Shared dashboard time-scope hook. Persists the chosen range to sessionStorage
 * so re-navigating the dashboard within a session keeps the user's selection.
 */
export function useDateRange(): UseDateRangeResult {
  const [range, setRangeState] = useState<DateRangeKey>(() => readPersistedRange());

  useEffect(() => {
    persistRange(range);
  }, [range]);

  const setRange = useCallback((next: DateRangeKey) => {
    setRangeState(next);
  }, []);

  const { start, end } = useMemo(() => computeBounds(range, new Date()), [range]);

  return { range, setRange, start, end };
}
