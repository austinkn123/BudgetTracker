import type { FormEvent } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import ModalActions from './ModalActions';

describe('ModalActions', () => {
  it('renders Cancel and the confirm action, wiring each to its handler', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    const onConfirm = vi.fn();
    render(<ModalActions onCancel={onCancel} onConfirm={onConfirm} confirmLabel="Save" />);

    await user.click(screen.getByRole('button', { name: 'Cancel' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onConfirm).toHaveBeenCalledOnce();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });

  it('shows the delete action only when onDelete is given, with a custom label', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();
    render(
      <ModalActions
        onCancel={vi.fn()}
        confirmLabel="Save"
        onDelete={onDelete}
        deleteLabel="Remove rule"
        cancelLabel="Back"
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Remove rule' }));

    expect(onDelete).toHaveBeenCalledOnce();
    expect(screen.getByRole('button', { name: 'Back' })).toBeInTheDocument();
  });

  it('disables every action while pending', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(
      <ModalActions
        onCancel={onCancel}
        onConfirm={onConfirm}
        confirmLabel="Save"
        onDelete={vi.fn()}
        isPending
      />,
    );

    for (const name of ['Cancel', 'Save', 'Delete']) {
      expect(screen.getByRole('button', { name })).toBeDisabled();
    }
    await user.click(screen.getByRole('button', { name: 'Save' }));
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('submits the enclosing form when confirmType is submit', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn((event: FormEvent) => event.preventDefault());
    render(
      <form onSubmit={onSubmit}>
        <ModalActions onCancel={vi.fn()} confirmLabel="Create" confirmType="submit" />
      </form>,
    );

    await user.click(screen.getByRole('button', { name: 'Create' }));

    expect(onSubmit).toHaveBeenCalledOnce();
  });

  // BUG (reported, not fixed here): Button does not default `type="button"`, so Cancel and
  // Delete are implicit submit buttons inside a <form> — clicking Cancel submits the form.
  // `it.fails` keeps the suite green; flip to `it` once fixed.
  it.fails('does not submit the enclosing form when Cancel is clicked', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn((event: FormEvent) => event.preventDefault());
    const onCancel = vi.fn();
    render(
      <form onSubmit={onSubmit}>
        <ModalActions onCancel={onCancel} confirmLabel="Create" confirmType="submit" />
      </form>,
    );

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
