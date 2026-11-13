import { useEffect, useRef, useState } from 'react';

interface UseInViewCallbackOptions {
  /** Off while there is nothing to do, e.g. a page is already loading or there are no more. */
  enabled: boolean;
  /** How far ahead of the viewport to fire, so the next page is usually in before you reach it. */
  rootMargin?: string;
}

/**
 * Calls `onInView` when the element behind the returned ref scrolls into (or near) the viewport.
 *
 * The observer is rebuilt whenever `enabled` flips back on. An IntersectionObserver reports the
 * current state on `observe()`, so a sentinel that is still visible after a short page lands fires
 * again instead of waiting for a scroll that will never come.
 *
 * Where IntersectionObserver is missing it does nothing; pair the sentinel with a button.
 */
export const useInViewCallback = (
  onInView: () => void,
  { enabled, rootMargin = '400px 0px' }: UseInViewCallbackOptions,
) => {
  const [node, setNode] = useState<Element | null>(null);
  const callback = useRef(onInView);

  useEffect(() => {
    callback.current = onInView;
  }, [onInView]);

  useEffect(() => {
    if (!node || !enabled || typeof IntersectionObserver === 'undefined') return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) callback.current();
      },
      { rootMargin },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [node, enabled, rootMargin]);

  return setNode;
};
