import type { ComponentProps, FormEvent } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import Button from './Button';

describe('Button', () => {
  it('renders an enabled primary button by default and fires onClick', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Save</Button>);

    const button = screen.getByRole('button', { name: 'Save' });
    expect(button).toBeEnabled();
    expect(button).toHaveClass('bg-primary');
    await user.click(button);

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('shows a spinner and is disabled while loading', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(
      <Button loading onClick={onClick} startIcon={<span data-testid="start" />} endIcon={<span data-testid="end" />}>
        Save
      </Button>,
    );

    const button = screen.getByRole('button', { name: 'Save' });
    expect(button).toBeDisabled();
    expect(button.querySelector('svg.animate-spin')).toBeInTheDocument();
    // The spinner replaces the start icon and suppresses the end icon.
    expect(screen.queryByTestId('start')).not.toBeInTheDocument();
    expect(screen.queryByTestId('end')).not.toBeInTheDocument();

    await user.click(button);
    expect(onClick).not.toHaveBeenCalled();
  });

  it('renders no spinner when not loading', () => {
    render(<Button startIcon={<span data-testid="start" />}>Save</Button>);

    const button = screen.getByRole('button', { name: 'Save' });
    expect(button.querySelector('.animate-spin')).not.toBeInTheDocument();
    expect(screen.getByTestId('start')).toBeInTheDocument();
  });

  it('uses the error token classes for the destructive variant', () => {
    render(<Button variant="destructive">Delete</Button>);

    const button = screen.getByRole('button', { name: 'Delete' });
    expect(button).toHaveClass('bg-error', 'border-error', 'text-white');
    expect(button).not.toHaveClass('bg-primary');
  });

  it('uses the error text token for destructive-ghost', () => {
    render(<Button variant="destructive-ghost">Remove</Button>);

    expect(screen.getByRole('button', { name: 'Remove' })).toHaveClass('text-error', 'bg-transparent');
  });

  it('stays disabled when disabled is set, regardless of loading', () => {
    render(<Button disabled>Save</Button>);

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
  });

  it('renders as another element through component, forwarding to', () => {
    // Stands in for a router Link: takes `to`, renders an anchor.
    const FakeLink = ({ to, ...props }: { to: string } & ComponentProps<'a'>) => <a href={to} {...props} />;
    render(
      <Button component={FakeLink} to="/plan">
        Plan
      </Button>,
    );

    expect(screen.getByRole('link', { name: 'Plan' })).toHaveAttribute('href', '/plan');
  });

  it('defaults to type="button" so it never submits an enclosing form implicitly', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn((event: FormEvent) => event.preventDefault());
    render(
      <form onSubmit={onSubmit}>
        <Button>Cancel</Button>
      </form>,
    );

    const button = screen.getByRole('button', { name: 'Cancel' });
    expect(button).toHaveAttribute('type', 'button');
    await user.click(button);

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('still submits the enclosing form when type="submit" is passed', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn((event: FormEvent) => event.preventDefault());
    render(
      <form onSubmit={onSubmit}>
        <Button type="submit">Save</Button>
      </form>,
    );

    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it('does not put a type attribute on a non-button element rendered through component', () => {
    const FakeLink = ({ to, ...props }: { to: string } & ComponentProps<'a'>) => <a href={to} {...props} />;
    render(
      <Button component={FakeLink} to="/plan">
        Plan
      </Button>,
    );

    expect(screen.getByRole('link', { name: 'Plan' })).not.toHaveAttribute('type');
  });

  it('marks itself aria-busy while loading, and not otherwise', () => {
    const { rerender } = render(<Button loading>Save</Button>);
    expect(screen.getByRole('button', { name: 'Save' })).toHaveAttribute('aria-busy', 'true');

    rerender(<Button>Save</Button>);
    expect(screen.getByRole('button', { name: 'Save' })).not.toHaveAttribute('aria-busy');
  });
});
