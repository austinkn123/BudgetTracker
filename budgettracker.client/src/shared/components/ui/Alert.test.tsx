import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import Alert from './Alert';

describe('Alert', () => {
  it('announces its title and message through role="alert"', () => {
    render(<Alert severity="success" title="Saved" message="Your plan was updated." />);

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Saved');
    expect(alert).toHaveTextContent('Your plan was updated.');
  });

  it('applies the semantic token classes for the severity and variant', () => {
    const { rerender } = render(<Alert severity="error" message="Failed" />);
    expect(screen.getByRole('alert')).toHaveClass('bg-error-subtle', 'border-error-light');

    rerender(<Alert severity="error" variant="outlined" message="Failed" />);
    expect(screen.getByRole('alert')).toHaveClass('bg-transparent', 'border-error');
    expect(screen.getByRole('alert')).not.toHaveClass('bg-error-subtle');
  });

  it('renders no dismiss button without onClose', () => {
    render(<Alert severity="info" message="FYI" />);

    expect(screen.queryByRole('button', { name: 'Dismiss' })).not.toBeInTheDocument();
  });

  it('calls onClose when the dismiss button is activated', async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    render(<Alert severity="warning" message="Heads up" onClose={onClose} />);

    await user.click(screen.getByRole('button', { name: 'Dismiss' }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
