import { describe, expect, it } from 'vitest';
import { mfaCodeSchema } from './auth';

describe('mfaCodeSchema', () => {
  it.each(['123456', ' 000000 '])('accepts %j', (code) => {
    expect(mfaCodeSchema.safeParse({ code }).success).toBe(true);
  });

  it.each(['', '12345', '1234567', '12a456', '12 456'])('rejects %j', (code) => {
    const result = mfaCodeSchema.safeParse({ code });
    expect(result.success).toBe(false);
    expect(result.error?.issues[0].message).toBe('Enter the 6-digit code');
  });
});
