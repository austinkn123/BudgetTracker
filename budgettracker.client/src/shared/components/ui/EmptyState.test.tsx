import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import Button from './Button';
import EmptyState, { ErrorState } from './EmptyState';

describe('EmptyState', () => {
  it('shows its title, description and action', () => {
    render(
      <EmptyState
        title="No bank linked yet"
        description="Link one to import transactions."
        action={<Button>Connect a bank</Button>}
      />,
    );

    expect(screen.getByText('No bank linked yet')).toBeInTheDocument();
    expect(screen.getByText('Link one to import transactions.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Connect a bank' })).toBeInTheDocument();
  });

  it('draws its own surface as a panel and none inline', () => {
    const { container, rerender } = render(<EmptyState title="Nothing here" />);
    expect(container.firstChild).toHaveClass('border', 'bg-surface');

    rerender(<EmptyState title="Nothing here" layout="inline" />);
    expect(container.firstChild).not.toHaveClass('border');
    expect(container.firstChild).not.toHaveClass('bg-surface');
  });

  it('is not announced unless a role is given', () => {
    render(<EmptyState title="Nothing here" />);

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});

describe('ErrorState', () => {
  it('announces the failure through role="alert" with a default explanation', () => {
    render(<ErrorState title="Couldn't load your rules" onRetry={() => undefined} />);

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent("Couldn't load your rules");
    expect(alert).toHaveTextContent("We couldn't reach the server. Try again in a moment.");
  });

  it('calls onRetry when Try again is pressed', async () => {
    const onRetry = vi.fn();
    render(<ErrorState title="Couldn't load your rules" onRetry={onRetry} />);

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('disables Try again while the retry is in flight', () => {
    render(<ErrorState title="Couldn't load your rules" onRetry={() => undefined} isRetrying />);

    expect(screen.getByRole('button', { name: 'Try again' })).toBeDisabled();
  });

  it('uses a description when one is given', () => {
    render(
      <ErrorState
        title="Couldn't load your holdings"
        description="Your balances will show once it loads."
        onRetry={() => undefined}
      />,
    );

    expect(screen.getByRole('alert')).toHaveTextContent('Your balances will show once it loads.');
    expect(screen.getByRole('alert')).not.toHaveTextContent("We couldn't reach the server.");
  });
});
