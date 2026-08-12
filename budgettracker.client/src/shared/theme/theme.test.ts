import { describe, expect, it } from 'vitest';
import {
  colorTokens,
  fontSizeTokens,
  hexToChannels,
  motionTokens,
  radiusTokens,
  shadowTokens,
  withAlpha,
} from './tokens';

/**
 * BUD-20 — token shape tests.
 * tokens.ts is the single source of truth for the design system; these guard
 * its contract now that the MUI theme is gone.
 */
describe('colorTokens', () => {
  it.each(['primary', 'secondary', 'success', 'warning', 'error', 'info'] as const)(
    '%s exposes main/light/dark/subtle/contrastText',
    (key) => {
      const scale = colorTokens[key];
      for (const shade of ['main', 'light', 'dark', 'subtle', 'contrastText'] as const) {
        expect(scale[shade]).toMatch(/^#[0-9A-Fa-f]{6}$/);
      }
    },
  );

  it('exposes the full neutral set including the border scale', () => {
    const { neutral } = colorTokens;
    for (const key of [
      'background',
      'surface',
      'borderSubtle',
      'border',
      'borderStrong',
      'textPrimary',
      'textSecondary',
    ] as const) {
      expect(neutral[key]).toMatch(/^#[0-9A-Fa-f]{6}$/);
    }
  });

  it('aligns the border scale with the grey ramp', () => {
    expect(colorTokens.neutral.borderSubtle).toBe(colorTokens.grey[100]);
    expect(colorTokens.neutral.border).toBe(colorTokens.grey[200]);
    expect(colorTokens.neutral.borderStrong).toBe(colorTokens.grey[300]);
  });
});

describe('color helpers', () => {
  it('converts hex to space-separated rgb channels', () => {
    expect(hexToChannels('#1E6FD9')).toBe('30 111 217');
    expect(hexToChannels('#FFFFFF')).toBe('255 255 255');
    expect(hexToChannels('#fff')).toBe('255 255 255');
  });

  it('produces hash-free alpha colors (hex-audit safe)', () => {
    const result = withAlpha(colorTokens.primary.main, 0.14);
    expect(result).toBe('rgb(30 111 217 / 0.14)');
    expect(result).not.toContain('#');
  });
});

describe('scale tokens (Stripe-leaning, BUD-20)', () => {
  it('clamps the radius scale at 12px', () => {
    const px = (v: string) => (v === '9999px' ? 9999 : Number.parseInt(v, 10));
    for (const value of Object.values(radiusTokens)) {
      if (value !== '9999px') {
        expect(px(value)).toBeLessThanOrEqual(12);
      }
    }
    expect(radiusTokens.DEFAULT).toBe('6px');
    expect(radiusTokens.md).toBe('8px');
  });

  it('defines four layered shadows referencing the shadow channel var', () => {
    for (const key of ['xs', 'sm', 'md', 'lg'] as const) {
      expect(shadowTokens[key]).toContain('var(--bud-shadow)');
    }
  });

  it('defines the motion scale', () => {
    expect(motionTokens.duration[120]).toBe('120ms');
    expect(motionTokens.duration[240]).toBe('240ms');
    expect(motionTokens.easing['out-soft']).toContain('cubic-bezier');
  });

  it('defines the extra font sizes', () => {
    expect(fontSizeTokens['2xs'][0]).toBe('0.6875rem');
    expect(fontSizeTokens.body[0]).toBe('0.9375rem');
  });
});
