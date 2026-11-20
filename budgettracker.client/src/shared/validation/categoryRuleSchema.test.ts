import { describe, expect, it } from 'vitest';

import { categoryRuleSchema } from './categoryRuleSchema';

const valid = { pattern: 'uber', matchType: 'Contains', categoryId: 7, applyToExisting: false } as const;

describe('categoryRuleSchema', () => {
  it('accepts a valid rule and trims the pattern', () => {
    const result = categoryRuleSchema.safeParse({ ...valid, pattern: '  uber eats ' });

    expect(result.success && result.data.pattern).toBe('uber eats');
  });

  it.each(['', '   '])('rejects a blank pattern (%j)', (pattern) => {
    const result = categoryRuleSchema.safeParse({ ...valid, pattern });

    expect(result.error?.issues[0]?.message).toBe('Pattern is required');
  });

  it('accepts 255 characters and rejects 256', () => {
    expect(categoryRuleSchema.safeParse({ ...valid, pattern: 'a'.repeat(255) }).success).toBe(true);
    expect(categoryRuleSchema.safeParse({ ...valid, pattern: 'a'.repeat(256) }).error?.issues[0]?.message).toBe(
      'Pattern must be 255 characters or fewer',
    );
  });

  it.each(['Regex', 'contains', ''])('rejects match type %j', (matchType) => {
    expect(categoryRuleSchema.safeParse({ ...valid, matchType }).success).toBe(false);
  });

  it.each([null, 0, -1, 1.5])('rejects category %j', (categoryId) => {
    expect(categoryRuleSchema.safeParse({ ...valid, categoryId }).error?.issues[0]?.message).toBe('Pick a category');
  });

  it('defaults applyToExisting to false', () => {
    const { applyToExisting: _omit, ...rest } = valid;
    void _omit;

    expect(categoryRuleSchema.parse(rest).applyToExisting).toBe(false);
  });
});
