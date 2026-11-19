import { afterEach, describe, expect, it, vi } from 'vitest';
import { localToday } from './localToday';

describe('localToday', () => {
  afterEach(() => vi.useRealTimers());

  it("is the browser's calendar date, even when UTC has already moved to tomorrow", () => {
    // 11:30 pm local on Nov 15 (built from local parts, so it holds in any zone the tests run in).
    vi.useFakeTimers({ now: new Date(2026, 10, 15, 23, 30) });

    expect(localToday()).toBe('2026-11-15');
  });

  it('pads month and day', () => {
    vi.useFakeTimers({ now: new Date(2026, 0, 5, 9, 0) });

    expect(localToday()).toBe('2026-01-05');
  });
});
