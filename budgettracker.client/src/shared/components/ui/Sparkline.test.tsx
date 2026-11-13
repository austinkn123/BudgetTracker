import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { cssVar } from '../../theme/tokens';
import Sparkline from './Sparkline';

const COLOR = cssVar('primary');

const paths = (container: HTMLElement) => Array.from(container.querySelectorAll('path'));

describe('Sparkline', () => {
  it('draws a decorative area and line through every point', () => {
    const { container } = render(<Sparkline data={[3, 8, 5, 10]} color={COLOR} height={40} />);

    const svg = container.querySelector('svg')!;
    expect(svg).toHaveAttribute('aria-hidden', 'true');
    expect(svg).toHaveAttribute('viewBox', '0 0 100 40');

    const [area, line] = paths(container);
    expect(line).toHaveAttribute('stroke', COLOR);
    // One cubic segment per gap between points.
    expect(line.getAttribute('d')!.match(/C /g)).toHaveLength(3);
    // The area fills from the gradient, referenced by its generated id.
    const gradientId = container.querySelector('linearGradient')!.id;
    expect(area).toHaveAttribute('fill', `url(#${gradientId})`);
  });

  it('maps the min to the bottom padding and the max to the top padding', () => {
    const { container } = render(<Sparkline data={[0, 10]} color={COLOR} height={40} />);

    // pad = 40 * 0.15 = 6, so the min sits at y = 34 and the max at y = 6.
    const d = paths(container)[1].getAttribute('d')!;
    expect(d.startsWith('M 0 34')).toBe(true);
    expect(d.endsWith('100 6')).toBe(true);
  });

  it.each([[[]], [[42]]])('renders nothing for fewer than two points (%j)', (data) => {
    const { container } = render(<Sparkline data={data} color={COLOR} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('draws a flat line along the bottom padding when every value is equal', () => {
    const { container } = render(<Sparkline data={[5, 5, 5]} color={COLOR} height={40} />);

    const d = paths(container)[1].getAttribute('d')!;
    expect(d).not.toContain('NaN');
    expect(d.startsWith('M 0 34')).toBe(true);
  });

  it('tints the gradient from the theme-aware token rather than freezing a fixed colour', () => {
    const { container } = render(<Sparkline data={[1, 2]} color={COLOR} />);

    const stops = Array.from(container.querySelectorAll('stop'));
    expect(stops.map((stop) => stop.getAttribute('stop-color'))).toEqual([
      cssVar('primary', 0.25),
      cssVar('primary', 0.02),
    ]);
  });
});
