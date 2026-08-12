import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Compose class names with Tailwind conflict resolution (BUD-20).
 * Later inputs win: `cn('p-4', className)` lets callers override defaults.
 */
export const cn = (...inputs: ClassValue[]): string => twMerge(clsx(inputs));
