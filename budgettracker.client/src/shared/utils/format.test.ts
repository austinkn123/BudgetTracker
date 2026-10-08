import { describe, expect, it } from 'vitest';
import { overAmount } from './format';

describe('overAmount', () => {
  it('keeps cents on small overages so they never read as $0', () => {
    expect(overAmount(0.4)).toBe('$0.40');
    expect(overAmount(9.99)).toBe('$9.99');
  });

  it('drops cents once the overage is large enough to read whole', () => {
    expect(overAmount(88.12)).toBe('$88');
  });
});
