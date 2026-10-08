import { describe, expect, it } from 'vitest';
import { errorMessage } from './errorMessage';

describe('errorMessage', () => {
  it('prefers the server error field', () => {
    expect(errorMessage({ response: { data: { error: 'Plan name is taken.' } } }, 'Fallback.')).toBe(
      'Plan name is taken.',
    );
  });

  it('never leaks exception text', () => {
    expect(errorMessage(new Error('Request failed with status code 500'), 'Fallback.')).toBe('Fallback.');
  });

  it('ignores a blank or non-string server error', () => {
    expect(errorMessage({ response: { data: { error: '  ' } } }, 'Fallback.')).toBe('Fallback.');
    expect(errorMessage({ response: { data: { error: { code: 1 } } } }, 'Fallback.')).toBe('Fallback.');
  });
});
