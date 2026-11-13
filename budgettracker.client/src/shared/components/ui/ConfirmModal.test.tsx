import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import ConfirmModal from './ConfirmModal';
import type { ConfirmModalProps } from './ConfirmModal';

const renderModal = (props: Partial<ConfirmModalProps> = {}) => {
  const onCancel = vi.fn();
  const onConfirm = vi.fn();
  render(
    <ConfirmModal
      open
      title="Delete category?"
      message="This cannot be undone."
      onCancel={onCancel}
      onConfirm={onConfirm}
      {...props}
    />,
  );
  return { onCancel, onConfirm };
};

describe('ConfirmModal', () => {
  it('renders a named dialog with its message and default actions', () => {
    renderModal();

    const dialog = screen.getByRole('dialog', { name: 'Delete category?' });
    expect(dialog).toHaveAccessibleDescription('This cannot be undone.');
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirm' })).toHaveClass('bg-primary');
  });

  it('calls onConfirm from the confirm button and onCancel from the cancel button', async () => {
    const user = userEvent.setup();
    const { onCancel, onConfirm } = renderModal({ confirmLabel: 'Delete', cancelLabel: 'Keep' });

    await user.click(screen.getByRole('button', { name: 'Delete' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Keep' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('cancels on Escape', async () => {
    const user = userEvent.setup();
    const { onCancel, onConfirm } = renderModal();

    await user.keyboard('{Escape}');

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('uses the destructive variant when destructive', () => {
    renderModal({ destructive: true, confirmLabel: 'Delete' });

    expect(screen.getByRole('button', { name: 'Delete' })).toHaveClass('bg-error');
  });

  it('locks both actions and Escape while pending', async () => {
    const user = userEvent.setup();
    const { onCancel, onConfirm } = renderModal({ isPending: true });

    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();

    await user.keyboard('{Escape}');
    expect(onCancel).not.toHaveBeenCalled();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('renders nothing when closed', () => {
    renderModal({ open: false });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
