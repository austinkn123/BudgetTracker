import { useEffect, useState, type RefObject } from 'react';

export type ScrollEdges = { start: boolean; end: boolean };

/**
 * Whether a horizontally scrolling element has content hidden past its start or end, kept current on scroll and
 * resize (BUD-29). Drives the edge fade that tells a phone user a row of options keeps going.
 */
export const useScrollEdges = <T extends HTMLElement>(ref: RefObject<T | null>): ScrollEdges => {
  const [edges, setEdges] = useState<ScrollEdges>({ start: false, end: false });

  useEffect(() => {
    const el = ref.current;
    if (!el) return;

    const update = () => {
      const hiddenAfter = el.scrollWidth - el.clientWidth - el.scrollLeft;
      const next = { start: el.scrollLeft > 1, end: hiddenAfter > 1 };
      setEdges((prev) => (prev.start === next.start && prev.end === next.end ? prev : next));
    };

    update();
    el.addEventListener('scroll', update, { passive: true });
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(update);
    observer?.observe(el);
    return () => {
      el.removeEventListener('scroll', update);
      observer?.disconnect();
    };
  }, [ref]);

  return edges;
};
