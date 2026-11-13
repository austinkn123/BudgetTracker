import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import Spinner from './Spinner';

describe('Spinner', () => {
  it('has an accessible "Loading" name', () => {
    render(<Spinner />);

    const spinner = screen.getByLabelText('Loading');
    expect(spinner).toHaveAttribute('width', '20');
  });

  it('honours a custom size and merges extra classes', () => {
    render(<Spinner size={32} className="text-primary" />);

    const spinner = screen.getByLabelText('Loading');
    expect(spinner).toHaveAttribute('width', '32');
    expect(spinner).toHaveAttribute('height', '32');
    expect(spinner).toHaveClass('animate-spin', 'text-primary');
  });
});
