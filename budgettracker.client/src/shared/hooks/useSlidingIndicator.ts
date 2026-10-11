import { useLayoutEffect, useRef, useState } from 'react';

export interface IndicatorRect {
  left: number;
  top: number;
  width: number;
  height: number;
}

/**
 * Measures the active item of a segmented control so one indicator can slide
 * between items instead of each item painting its own background.
 *
 * The container must be positioned (`relative`): the rect is read from the
 * active element's offset box, which is relative to its offsetParent. A
 * ResizeObserver re-measures as the container or active item resizes, so the
 * indicator follows layout morphs (the nav contracting) with no extra wiring.
 *
 * Returns `null` until there is a measurement (or no item is active, or the
 * container is hidden), so callers render nothing rather than flying in from 0.
 */
export const useSlidingIndicator = <T extends HTMLElement>(
  /** Matches the active item inside the container, e.g. `[aria-current="page"]`. */
  activeSelector: string,
  /** Changes whenever the active item does, to trigger a re-measure. */
  activeKey: unknown,
) => {
  const containerRef = useRef<T>(null);
  const [rect, setRect] = useState<IndicatorRect | null>(null);

  useLayoutEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    const active = container.querySelector<HTMLElement>(activeSelector);

    const measure = () => {
      const next =
        active && active.offsetWidth > 0
          ? {
              left: active.offsetLeft,
              top: active.offsetTop,
              width: active.offsetWidth,
              height: active.offsetHeight,
            }
          : null;
      setRect((prev) =>
        prev && next &&
        prev.left === next.left && prev.top === next.top &&
        prev.width === next.width && prev.height === next.height
          ? prev
          : next,
      );
    };

    measure();
    // jsdom has no ResizeObserver; the single measure above is enough there.
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(measure);
    observer.observe(container);
    if (active) observer.observe(active);
    return () => observer.disconnect();
  }, [activeSelector, activeKey]);

  return { containerRef, rect };
};
