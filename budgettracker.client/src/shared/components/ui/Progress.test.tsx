import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import Progress from './Progress';

const bar = () => screen.getByRole('progressbar');

describe('Progress', () => {
  it('exposes the value as a 0–100 progressbar', () => {
    render(<Progress value={42} />);

    expect(bar()).toHaveAttribute('aria-valuenow', '42');
    expect(bar()).toHaveAttribute('aria-valuemin', '0');
    expect(bar()).toHaveAttribute('aria-valuemax', '100');
  });

  it('updates the aria value when the prop changes', () => {
    const { rerender } = render(<Progress value={10} />);

    rerender(<Progress value={75} />);

    expect(bar()).toHaveAttribute('aria-valuenow', '75');
  });

  it.each([
    [150, '100'],
    [-20, '0'],
  ])('clamps out-of-range input %d to %s', (value, expected) => {
    render(<Progress value={value} />);

    expect(bar()).toHaveAttribute('aria-valuenow', expected);
  });

  it('reports the boundaries as complete and empty', () => {
    const { rerender } = render(<Progress value={100} />);
    expect(bar()).toHaveAttribute('data-state', 'complete');

    rerender(<Progress value={0} />);
    expect(bar()).toHaveAttribute('data-state', 'loading');
  });
});
