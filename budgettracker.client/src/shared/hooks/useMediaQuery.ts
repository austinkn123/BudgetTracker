import { useCallback, useSyncExternalStore } from 'react';

/** Phones: below Tailwind's `sm` breakpoint. */
export const PHONE_QUERY = '(max-width: 639px)';

/** Whether a CSS media query matches, kept in sync as the viewport changes. False where matchMedia is missing. */
export const useMediaQuery = (query: string) => {
  const subscribe = useCallback(
    (onChange: () => void) => {
      if (typeof window.matchMedia !== 'function') return () => {};
      const list = window.matchMedia(query);
      list.addEventListener('change', onChange);
      return () => list.removeEventListener('change', onChange);
    },
    [query],
  );
  return useSyncExternalStore(
    subscribe,
    () => typeof window.matchMedia === 'function' && window.matchMedia(query).matches,
    () => false,
  );
};
