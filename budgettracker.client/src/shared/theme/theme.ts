/**
 * Theme preference runtime (BUD-17).
 *
 * The visual switch is entirely a token swap — `tailwind.config.ts` emits the
 * light values on `:root` and the dark values on `.dark`, so this module's only
 * job is deciding whether that class is on `<html>` and remembering the choice.
 *
 * Deliberately framework-free so `index.html` can inline the same logic before
 * first paint (see the bootstrap script there) and avoid a light flash.
 */

/** What the user picked. `system` defers to the OS setting, and keeps tracking it. */
export type ThemePreference = 'light' | 'dark' | 'system';

/** What actually gets painted once `system` is resolved. */
export type ResolvedTheme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'bud-theme';

export const THEME_PREFERENCES: readonly ThemePreference[] = ['light', 'dark', 'system'];

const isPreference = (value: unknown): value is ThemePreference =>
  value === 'light' || value === 'dark' || value === 'system';

/** Reads the saved choice. Falls back to `system` when absent or unreadable. */
export const readStoredPreference = (): ThemePreference => {
  try {
    const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
    return isPreference(stored) ? stored : 'system';
  } catch {
    // Private mode / blocked storage — the preference just doesn't persist.
    return 'system';
  }
};

export const storePreference = (preference: ThemePreference): void => {
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, preference);
  } catch {
    // Non-fatal: the theme still applies for this session.
  }
};

export const prefersDark = (): boolean =>
  typeof window.matchMedia === 'function' &&
  window.matchMedia('(prefers-color-scheme: dark)').matches;

export const resolveTheme = (preference: ThemePreference): ResolvedTheme =>
  preference === 'system' ? (prefersDark() ? 'dark' : 'light') : preference;

/**
 * Applies the resolved theme to `<html>`. The class drives Tailwind's `dark:`
 * variant AND the `.dark` custom-property block, so this single toggle is the
 * whole switch.
 */
export const applyTheme = (theme: ResolvedTheme): void => {
  document.documentElement.classList.toggle('dark', theme === 'dark');
};
