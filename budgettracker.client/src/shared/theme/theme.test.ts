import { describe, expect, it } from 'vitest';
import { budgetTrackerTheme } from './theme';
import { colorTokens } from './tokens';

/**
 * BUD-13 — theme shape test.
 * Guards the token contract: palette keys exist and resolve to the values in
 * tokens.ts (the same file tailwind.config.ts consumes).
 */
describe('budgetTrackerTheme', () => {
  it('defines brand colors from the token file', () => {
    expect(budgetTrackerTheme.palette.primary.main).toBe(colorTokens.primary.main);
    expect(budgetTrackerTheme.palette.secondary.main).toBe(colorTokens.secondary.main);
  });

  it('defines all semantic colors from the token file', () => {
    expect(budgetTrackerTheme.palette.success.main).toBe(colorTokens.success.main);
    expect(budgetTrackerTheme.palette.warning.main).toBe(colorTokens.warning.main);
    expect(budgetTrackerTheme.palette.error.main).toBe(colorTokens.error.main);
    expect(budgetTrackerTheme.palette.info.main).toBe(colorTokens.info.main);
  });

  it('exposes light/dark/subtle/contrastText shades on each palette color', () => {
    for (const key of ['primary', 'secondary', 'success', 'warning', 'error', 'info'] as const) {
      const color = budgetTrackerTheme.palette[key];
      expect(color.light).toBe(colorTokens[key].light);
      expect(color.dark).toBe(colorTokens[key].dark);
      expect(color.subtle).toBe(colorTokens[key].subtle);
      expect(color.contrastText).toBe(colorTokens[key].contrastText);
    }
  });

  it('defines neutral background, surface, border, and text tokens', () => {
    expect(budgetTrackerTheme.palette.background.default).toBe(colorTokens.neutral.background);
    expect(budgetTrackerTheme.palette.background.paper).toBe(colorTokens.neutral.surface);
    expect(budgetTrackerTheme.palette.divider).toBe(colorTokens.neutral.border);
    expect(budgetTrackerTheme.palette.text.primary).toBe(colorTokens.neutral.textPrimary);
    expect(budgetTrackerTheme.palette.text.secondary).toBe(colorTokens.neutral.textSecondary);
  });

  it('uses Inter as the primary font family', () => {
    expect(budgetTrackerTheme.typography.fontFamily).toMatch(/^"Inter"/);
    for (const variant of ['h1', 'h2', 'h3', 'h4', 'body1', 'body2', 'caption', 'overline'] as const) {
      const { fontSize, fontWeight, lineHeight } = budgetTrackerTheme.typography[variant];
      expect(fontSize).toBeDefined();
      expect(fontWeight).toBeDefined();
      expect(lineHeight).toBeDefined();
    }
  });

  it('uses an 8px spacing base unit', () => {
    expect(budgetTrackerTheme.spacing(1)).toBe('8px');
  });
});
