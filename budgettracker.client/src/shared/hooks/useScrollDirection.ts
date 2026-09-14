import { useEffect, useRef, useState } from 'react';

export type ScrollDirection = 'up' | 'down';

export interface ScrollState {
  direction: ScrollDirection;
  /** True while the page is within `topThreshold` of the top. */
  atTop: boolean;
}

interface UseScrollDirectionOptions {
  /** Ignore movements smaller than this, so a trackpad twitch can't flip state. */
  delta?: number;
  /** Below this scroll offset we always report `atTop`. */
  topThreshold?: number;
}

/**
 * Tracks scroll direction with rAF throttling (BUD-20).
 *
 * Reads are batched into an animation frame so a fast scroll produces at most
 * one state update per paint, and a `delta` deadband keeps the direction from
 * oscillating during momentum scrolling.
 */
export const useScrollDirection = ({
  delta = 8,
  topThreshold = 96,
}: UseScrollDirectionOptions = {}): ScrollState => {
  const [state, setState] = useState<ScrollState>({ direction: 'up', atTop: true });
  const lastY = useRef(0);
  const ticking = useRef(false);

  useEffect(() => {
    lastY.current = window.scrollY;

    const evaluate = () => {
      ticking.current = false;
      const y = Math.max(0, window.scrollY);
      const atTop = y <= topThreshold;

      // Deadband: keep the previous direction until movement is decisive.
      if (Math.abs(y - lastY.current) < delta) {
        setState((prev) => (prev.atTop === atTop ? prev : { ...prev, atTop }));
        return;
      }

      const direction: ScrollDirection = y > lastY.current ? 'down' : 'up';
      lastY.current = y;
      setState((prev) =>
        prev.direction === direction && prev.atTop === atTop ? prev : { direction, atTop },
      );
    };

    const onScroll = () => {
      if (ticking.current) return;
      ticking.current = true;
      window.requestAnimationFrame(evaluate);
    };

    evaluate();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, [delta, topThreshold]);

  return state;
};
