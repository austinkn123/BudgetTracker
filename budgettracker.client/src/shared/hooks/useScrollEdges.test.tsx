import { act, render, screen } from '@testing-library/react';
import { useRef } from 'react';
import { describe, expect, it } from 'vitest';
import { useScrollEdges } from './useScrollEdges';

const Probe = ({ scrollWidth, clientWidth }: { scrollWidth: number; clientWidth: number }) => {
  const ref = useRef<HTMLDivElement>(null);
  const edges = useScrollEdges(ref);
  return (
    <div
      data-testid="box"
      ref={(el) => {
        ref.current = el;
        if (el) {
          Object.defineProperty(el, 'scrollWidth', { configurable: true, value: scrollWidth });
          Object.defineProperty(el, 'clientWidth', { configurable: true, value: clientWidth });
        }
      }}
    >
      {`${edges.start}/${edges.end}`}
    </div>
  );
};

describe('useScrollEdges', () => {
  it('reports nothing hidden when the content fits', () => {
    render(<Probe scrollWidth={200} clientWidth={200} />);
    expect(screen.getByTestId('box')).toHaveTextContent('false/false');
  });

  it('reports content hidden past the end, then past the start once scrolled', () => {
    render(<Probe scrollWidth={300} clientWidth={200} />);
    const box = screen.getByTestId('box');
    expect(box).toHaveTextContent('false/true');

    act(() => {
      box.scrollLeft = 50;
      box.dispatchEvent(new Event('scroll'));
    });
    expect(box).toHaveTextContent('true/true');

    act(() => {
      box.scrollLeft = 100;
      box.dispatchEvent(new Event('scroll'));
    });
    expect(box).toHaveTextContent('true/false');
  });
});
