import { createContext } from 'react';
import type { ResolvedTheme, ThemePreference } from './theme';

export type ThemeContextType = {
  /** What the user chose — including `system`. */
  preference: ThemePreference;
  /** What is actually painted right now. */
  theme: ResolvedTheme;
  setPreference: (preference: ThemePreference) => void;
  /** Flips between light and dark, resolving `system` to its opposite first. */
  toggle: () => void;
};

/**
 * Split from the provider so the provider file exports only a component —
 * `react-refresh/only-export-components` fails a mixed module.
 */
export const ThemeContext = createContext<ThemeContextType | undefined>(undefined);
