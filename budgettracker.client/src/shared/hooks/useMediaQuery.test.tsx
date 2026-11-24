import { act, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useMediaQuery } from './useMediaQuery';

const Probe = () => <p>{String(useMediaQuery('(max-width: 639px)'))}</p>;

/** A controllable matchMedia: `set` flips the match and notifies listeners like a viewport resize. */
const stubMatchMedia = (initial: boolean) => {
  let matches = initial;
  const listeners = new Set<() => void>();
  vi.stubGlobal('matchMedia', (query: string) => ({
    get matches() {
      return matches;
    },
    media: query,
    addEventListener: (_: string, fn: () => void) => listeners.add(fn),
    removeEventListener: (_: string, fn: () => void) => listeners.delete(fn),
  }));
  return {
    set: (next: boolean) => {
      matches = next;
      listeners.forEach((fn) => fn());
    },
    listenerCount: () => listeners.size,
  };
};

describe('useMediaQuery', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('reports whether the query matches', () => {
    stubMatchMedia(true);
    render(<Probe />);
    expect(screen.getByText('true')).toBeInTheDocument();
  });

  it('updates when the viewport crosses the query', () => {
    const media = stubMatchMedia(false);
    render(<Probe />);
    act(() => media.set(true));
    expect(screen.getByText('true')).toBeInTheDocument();
  });

  it('stops listening on unmount', () => {
    const media = stubMatchMedia(false);
    const { unmount } = render(<Probe />);
    unmount();
    expect(media.listenerCount()).toBe(0);
  });

  it('reports false where matchMedia is missing', () => {
    vi.stubGlobal('matchMedia', undefined);
    render(<Probe />);
    expect(screen.getByText('false')).toBeInTheDocument();
  });
});
