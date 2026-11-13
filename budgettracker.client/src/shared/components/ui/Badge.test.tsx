import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import Badge from './Badge';

describe('Badge', () => {
  it('renders a static, non-interactive pill with soft neutral defaults', () => {
    render(<Badge label="Pending" />);

    const label = screen.getByText('Pending');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(label.parentElement?.tagName).toBe('SPAN');
    expect(label.parentElement).toHaveClass('bg-border-subtle', 'text-ink-muted', 'h-6');
  });

  it('applies the colour/variant token classes', () => {
    render(<Badge label="Over" color="error" variant="solid" size="md" />);

    expect(screen.getByText('Over').parentElement).toHaveClass('bg-error', 'text-white', 'h-7');
  });

  it('becomes a button when onClick is given, and fires it', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(<Badge label="Groceries" onClick={onClick} />);

    const button = screen.getByRole('button', { name: 'Groceries' });
    expect(button).toHaveAttribute('type', 'button');
    await user.click(button);

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('fires onDelete from the remove button without triggering onClick', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();
    const onClick = vi.fn();
    // Clickable root plus a delete button: the delete must not bubble into onClick.
    render(<Badge label="Tag" onClick={onClick} onDelete={onDelete} />);

    await user.click(screen.getByRole('button', { name: 'Remove' }));

    expect(onDelete).toHaveBeenCalledTimes(1);
    expect(onClick).not.toHaveBeenCalled();
  });
});
