import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { cssVar } from '../../theme/tokens';
import Gauge from './Gauge';

const COLORS = { valueColor: cssVar('primary'), textColor: cssVar('ink') };

describe('Gauge', () => {
  it('exposes the value as a 0-100 meter and prints it', () => {
    render(<Gauge value={42.4} {...COLORS} />);

    const meter = screen.getByRole('meter');
    expect(meter).toHaveAttribute('aria-valuemin', '0');
    expect(meter).toHaveAttribute('aria-valuemax', '100');
    expect(meter).toHaveAttribute('aria-valuenow', '42');
    expect(screen.getByText('42%')).toBeInTheDocument();
    expect(meter.querySelectorAll('path')).toHaveLength(2);
  });

  it('clamps out-of-range values', () => {
    const { rerender } = render(<Gauge value={150} {...COLORS} />);
    expect(screen.getByRole('meter')).toHaveAttribute('aria-valuenow', '100');
    expect(screen.getByText('100%')).toBeInTheDocument();

    rerender(<Gauge value={-20} {...COLORS} />);
    expect(screen.getByRole('meter')).toHaveAttribute('aria-valuenow', '0');
    expect(screen.getByText('0%')).toBeInTheDocument();
  });

  it('draws only the track at zero', () => {
    render(<Gauge value={0} {...COLORS} />);

    expect(screen.getByRole('meter').querySelectorAll('path')).toHaveLength(1);
  });

  it('uses the given track colour, sized to the size prop', () => {
    render(<Gauge value={50} size={120} trackColor={cssVar('border')} {...COLORS} />);

    const meter = screen.getByRole('meter');
    expect(meter).toHaveAttribute('width', '120');
    expect(meter.querySelector('path')).toHaveAttribute('stroke', cssVar('border'));
  });
});
