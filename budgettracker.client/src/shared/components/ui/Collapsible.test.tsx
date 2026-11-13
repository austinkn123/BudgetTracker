import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import Collapsible from './Collapsible';

describe('Collapsible', () => {
  it('renders its content when open', () => {
    render(
      <Collapsible open>
        <p>Details</p>
      </Collapsible>,
    );

    const details = screen.getByText('Details');
    expect(details).toBeVisible();
    expect(details.parentElement).toHaveAttribute('data-state', 'open');
  });

  it('removes its content when closed, and restores it on reopen', () => {
    const { rerender } = render(
      <Collapsible open={false}>
        <p>Details</p>
      </Collapsible>,
    );
    expect(screen.queryByText('Details')).not.toBeInTheDocument();

    rerender(
      <Collapsible open>
        <p>Details</p>
      </Collapsible>,
    );
    expect(screen.getByText('Details')).toBeInTheDocument();
  });

  it('merges a caller className onto the animated region', () => {
    render(
      <Collapsible open className="mt-2">
        <p>Details</p>
      </Collapsible>,
    );

    expect(screen.getByText('Details').parentElement).toHaveClass('mt-2', 'overflow-hidden');
  });
});
