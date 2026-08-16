import { clsx, type ClassValue } from 'clsx';
import { extendTailwindMerge } from 'tailwind-merge';

/**
 * tailwind-merge only knows Tailwind's stock scales. Our custom font sizes
 * (`text-2xs`, `text-body` from tokens.ts) would otherwise be classified as
 * text *colors*, putting them in the same conflict group as `text-white` —
 * which silently dropped the color and left dark text on dark buttons.
 * Registering them under font-size keeps the two groups independent.
 */
const twMerge = extendTailwindMerge({
  extend: {
    classGroups: {
      'font-size': [{ text: ['2xs', 'body'] }],
    },
  },
});

/**
 * Compose class names with Tailwind conflict resolution (BUD-20).
 * Later inputs win: `cn('p-4', className)` lets callers override defaults.
 */
export const cn = (...inputs: ClassValue[]): string => twMerge(clsx(inputs));
