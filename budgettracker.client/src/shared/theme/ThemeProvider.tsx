import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  applyTheme,
  readStoredPreference,
  resolveTheme,
  storePreference,
  type ResolvedTheme,
  type ThemePreference,
} from './theme';
import { ThemeContext } from './themeContext';

type ThemeProviderProps = {
  children: ReactNode;
};

/**
 * Owns the theme preference (BUD-17). The visual switch itself is a token swap
 * in `tailwind.config.ts`; this only decides whether `.dark` is on `<html>`.
 */
export const ThemeProvider = ({ children }: ThemeProviderProps) => {
  // index.html's bootstrap script has already applied the class before paint;
  // reading the same source here keeps React's first render agreeing with the DOM.
  const [preference, setPreferenceState] = useState<ThemePreference>(readStoredPreference);
  const [theme, setTheme] = useState<ResolvedTheme>(() => resolveTheme(readStoredPreference()));

  useEffect(() => {
    const resolved = resolveTheme(preference);
    setTheme(resolved);
    applyTheme(resolved);
  }, [preference]);

  // Only while on `system`: follow the OS if it changes mid-session.
  useEffect(() => {
    if (preference !== 'system' || typeof window.matchMedia !== 'function') return;

    const query = window.matchMedia('(prefers-color-scheme: dark)');
    const handleChange = () => {
      const resolved = resolveTheme('system');
      setTheme(resolved);
      applyTheme(resolved);
    };

    query.addEventListener('change', handleChange);
    return () => query.removeEventListener('change', handleChange);
  }, [preference]);

  const setPreference = useCallback((next: ThemePreference) => {
    storePreference(next);
    setPreferenceState(next);
  }, []);

  const toggle = useCallback(() => {
    setPreference(resolveTheme(preference) === 'dark' ? 'light' : 'dark');
  }, [preference, setPreference]);

  const value = useMemo(
    () => ({ preference, theme, setPreference, toggle }),
    [preference, theme, setPreference, toggle],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
};
