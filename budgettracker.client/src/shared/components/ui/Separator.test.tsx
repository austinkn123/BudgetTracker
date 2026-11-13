import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import Separator from './Separator';

describe('Separator', () => {
  it('renders a horizontal separator by default', () => {
    render(<Separator />);

    // Radix omits aria-orientation for horizontal, the ARIA default.
    const separator = screen.getByRole('separator');
    expect(separator).not.toHaveAttribute('aria-orientation');
    expect(separator).toHaveAttribute('data-orientation', 'horizontal');
  });

  it('announces a vertical orientation', () => {
    render(<Separator orientation="vertical" />);

    const separator = screen.getByRole('separator');
    expect(separator).toHaveAttribute('aria-orientation', 'vertical');
    expect(separator).toHaveAttribute('data-orientation', 'vertical');
  });
});
