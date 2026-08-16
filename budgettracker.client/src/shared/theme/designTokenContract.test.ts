import { readdirSync, readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { describe, expect, it } from 'vitest';
import tailwindConfig from '../../../tailwind.config';
import { colorTokens, hexToChannels } from './tokens';

/**
 * BUD-13 / BUD-20 — design-token contract tests (QA guard rails).
 *
 * 1. Tailwind parity: color utilities must be CSS-var references whose
 *    emitted channels come from tokens.ts. Catches copy-drift if someone
 *    replaces the token import with hardcoded values.
 * 2. Hex audit: no hardcoded hex colors outside the theme folder.
 *    Catches regressions where a component bypasses the token system.
 */

const twColors = (tailwindConfig.theme?.extend?.colors ?? {}) as Record<
  string,
  string | Record<string, string>
>;

const varRefPattern = (name: string) =>
  new RegExp(`^rgb\\(var\\(--bud-${name}\\) / <alpha-value>\\)$`);

describe('tailwind config token parity', () => {
  it.each(['primary', 'secondary', 'success', 'warning', 'error', 'info'] as const)(
    'tailwind %s scale references the token CSS variables',
    (key) => {
      const scale = twColors[key] as Record<string, string>;
      expect(scale.DEFAULT).toMatch(varRefPattern(key));
      expect(scale.light).toMatch(varRefPattern(`${key}-light`));
      expect(scale.dark).toMatch(varRefPattern(`${key}-dark`));
      expect(scale.subtle).toMatch(varRefPattern(`${key}-subtle`));
    },
  );

  it('maps semantic neutral utilities to token CSS variables', () => {
    expect(twColors.background).toMatch(varRefPattern('background'));
    expect(twColors.surface).toMatch(varRefPattern('surface'));
    const border = twColors.border as Record<string, string>;
    expect(border.subtle).toMatch(varRefPattern('border-subtle'));
    expect(border.DEFAULT).toMatch(varRefPattern('border'));
    expect(border.strong).toMatch(varRefPattern('border-strong'));
    const ink = twColors.ink as Record<string, string>;
    expect(ink.DEFAULT).toMatch(varRefPattern('ink'));
    expect(ink.muted).toMatch(varRefPattern('ink-muted'));
  });

  it('uses Inter as the leading sans font', () => {
    const sans = tailwindConfig.theme?.extend?.fontFamily?.sans as string[];
    expect(sans[0]).toBe('Inter');
  });

  it('channel conversion round-trips the brand tokens', () => {
    // The :root emitter derives channels via hexToChannels; spot-check the math
    // so a broken converter can't silently shift every color.
    expect(hexToChannels(colorTokens.primary.main)).toBe('99 91 255');
    expect(hexToChannels(colorTokens.neutral.textPrimary)).toBe('10 37 64');
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
