import { readdirSync, readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { describe, expect, it } from 'vitest';
import tailwindConfig from '../../../tailwind.config';
import { colorTokens } from './tokens';

/**
 * BUD-13 — design-token contract tests (QA guard rails).
 *
 * 1. Tailwind parity: tailwind.config.ts must resolve to the exact same values
 *    as tokens.ts (AC5). Catches copy-drift if someone replaces the token
 *    import with hardcoded values.
 * 2. Hex audit: no hardcoded hex colors outside the theme folder (AC2 / AC3).
 *    Catches regressions where a component bypasses the token system.
 */

type ColorScale = { DEFAULT: string; light: string; dark: string; subtle: string };
const twColors = (tailwindConfig.theme?.extend?.colors ?? {}) as Record<
  string,
  string | Record<string, string>
>;

describe('tailwind config token parity', () => {
  it.each(['primary', 'secondary', 'success', 'warning', 'error', 'info'] as const)(
    'tailwind %s scale matches the MUI token values',
    (key) => {
      const scale = twColors[key] as ColorScale;
      expect(scale.DEFAULT).toBe(colorTokens[key].main);
      expect(scale.light).toBe(colorTokens[key].light);
      expect(scale.dark).toBe(colorTokens[key].dark);
      expect(scale.subtle).toBe(colorTokens[key].subtle);
    },
  );

  it('maps semantic neutral utilities to the neutral tokens', () => {
    expect(twColors.background).toBe(colorTokens.neutral.background);
    expect(twColors.surface).toBe(colorTokens.neutral.surface);
    expect(twColors.border).toBe(colorTokens.neutral.border);
    expect((twColors.ink as Record<string, string>).DEFAULT).toBe(colorTokens.neutral.textPrimary);
    expect((twColors.ink as Record<string, string>).muted).toBe(colorTokens.neutral.textSecondary);
  });

  it('uses Inter as the leading sans font, matching the MUI theme', () => {
    const sans = tailwindConfig.theme?.extend?.fontFamily?.sans as string[];
    expect(sans[0]).toBe('Inter');
  });
});

describe('hardcoded hex audit (src outside shared/theme)', () => {
  const SRC_ROOT = join(__dirname, '..', '..');
  const HEX_PATTERN = /#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b/g;

  /**
   * Allowlist for files pinned as known violations while migration is in
   * flight. Empty since the chartTheme.ts fix — do NOT add new entries here.
   */
  const KNOWN_VIOLATIONS = new Set<string>([]);

  const collectSourceFiles = (dir: string): string[] =>
    readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) {
        // The theme folder is the one place hex values are allowed.
        return entry.name === 'theme' && dir.endsWith(`${sep}shared`) ? [] : collectSourceFiles(full);
      }
      return /\.(tsx|ts)$/.test(entry.name) && !/\.test\./.test(entry.name) ? [full] : [];
    });

  it('contains no hardcoded hex colors in .ts/.tsx files', () => {
    const offenders = collectSourceFiles(SRC_ROOT)
      .filter((file) => {
        const rel = relative(SRC_ROOT, file).split(sep).join('/');
        return !KNOWN_VIOLATIONS.has(rel);
      })
      .flatMap((file) => {
        const matches = readFileSync(file, 'utf8').match(HEX_PATTERN);
        return matches ? [`${relative(SRC_ROOT, file)}: ${matches.join(', ')}`] : [];
      });

    expect(offenders).toEqual([]);
  });
});
