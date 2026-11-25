import { afterEach, describe, expect, it } from 'vitest';
import { readStoredPreference, resolveTheme, THEME_STORAGE_KEY } from './theme';

describe('theme preference', () => {
  afterEach(() => window.localStorage.clear());

  it('defaults to dark when nothing is saved', () => {
    expect(readStoredPreference()).toBe('dark');
    expect(resolveTheme(readStoredPreference())).toBe('dark');
  });

  it('keeps a saved light choice', () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, 'light');
    expect(readStoredPreference()).toBe('light');
  });
});
