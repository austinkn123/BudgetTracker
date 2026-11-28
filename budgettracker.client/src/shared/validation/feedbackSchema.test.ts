import { describe, expect, it } from 'vitest';
import { FEEDBACK_MAX_LENGTH, feedbackSchema } from './feedbackSchema';

const errorOf = (message: unknown) => {
  const result = feedbackSchema.safeParse({ message });
  return result.success ? undefined : result.error.issues[0]?.message;
};

describe('feedbackSchema', () => {
  it('accepts and trims a message', () => {
    expect(feedbackSchema.parse({ message: '  love it  ' })).toEqual({ message: 'love it' });
  });

  it.each(['', '   '])('rejects an empty message %j', (message) => {
    expect(errorOf(message)).toBe("Feedback can't be empty");
  });

  it('accepts a message exactly at the limit', () => {
    expect(errorOf('a'.repeat(FEEDBACK_MAX_LENGTH))).toBeUndefined();
  });

  it('rejects a message over the limit', () => {
    expect(errorOf('a'.repeat(FEEDBACK_MAX_LENGTH + 1))).toBe('Feedback must be 2000 characters or fewer');
  });

  it('rejects a non-string message', () => {
    expect(errorOf(42)).toBeDefined();
  });
});
