import { describe, expect, it } from 'vitest';
import { getStatusHeadline } from './planCopy';

const JUNE = '2026-06-01';

describe('getStatusHeadline', () => {
  it('reports a not-yet-started month regardless of pacing', () => {
    expect(getStatusHeadline(0, 0, JUNE)).toBe('June is just getting started');
    expect(getStatusHeadline(0.9, 0, JUNE)).toBe('June is just getting started');
  });

  it.each([
    [-0.5, "You're cruising in June"],
    [-0.15, "You're cruising in June"],      // upper boundary of "cruising"
    [-0.149, 'Comfortably ahead in June'],
    [-0.05, 'Comfortably ahead in June'],    // the boundary the C# test originally tripped on
    [-0.049, 'Right on the trail for June'],
    [0, 'Right on the trail for June'],
    [0.049, 'Right on the trail for June'],
    [0.05, 'A little brisk in June'],
    [0.149, 'A little brisk in June'],
    [0.15, 'Tightening up needed in June'],
    [0.299, 'Tightening up needed in June'],
    [0.3, 'Off the trail in June'],
    [1, 'Off the trail in June'],
  ])('maps pacingDelta %s to its band', (pacingDelta, expected) => {
    expect(getStatusHeadline(pacingDelta, 0.5, JUNE)).toBe(expected);
  });

  it('names the plan month, not the current month', () => {
    expect(getStatusHeadline(0, 0.5, '2026-11-01')).toBe('Right on the trail for November');
  });
});
