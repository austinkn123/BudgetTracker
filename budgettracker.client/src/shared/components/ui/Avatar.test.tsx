import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import Avatar from './Avatar';

describe('Avatar', () => {
  it('renders its initials fallback', async () => {
    render(<Avatar>AK</Avatar>);

    // delayMs={0} still defers the Radix fallback by one tick, so it is not in the first paint.
    expect(await screen.findByText('AK')).toBeInTheDocument();
  });

  it('defaults to a 36px circle and honours a custom size', async () => {
    const { rerender } = render(<Avatar>AK</Avatar>);
    await screen.findByText('AK');
    const root = () => screen.getByText('AK').parentElement as HTMLElement;
    expect(root()).toHaveStyle({ width: '36px', height: '36px' });
    expect(root()).toHaveClass('rounded-full');

    rerender(<Avatar size={24}>AK</Avatar>);
    expect(root()).toHaveStyle({ width: '24px', height: '24px' });
  });

  it('merges a caller className without dropping the base classes', async () => {
    render(<Avatar className="ml-2">AK</Avatar>);

    expect((await screen.findByText('AK')).parentElement).toHaveClass('ml-2', 'rounded-full');
  });
});
