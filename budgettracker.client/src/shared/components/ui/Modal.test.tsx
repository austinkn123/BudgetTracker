import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import Button from './Button';
import Modal from './Modal';
import type { ModalProps } from './Modal';

const renderModal = (props: Partial<ModalProps> = {}) => {
  const onClose = vi.fn();
  render(
    <Modal open onClose={onClose} title="Edit budget" {...props}>
      <input aria-label="Amount" />
    </Modal>,
  );
  return { onClose };
};

describe('Modal', () => {
  it('renders a dialog named by its title with description, body and actions', () => {
    renderModal({ description: 'Change the monthly target.', actions: <Button>Save</Button> });

    const dialog = screen.getByRole('dialog', { name: 'Edit budget' });
    expect(dialog).toHaveAccessibleDescription('Change the monthly target.');
    expect(screen.getByRole('textbox', { name: 'Amount' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save' })).toBeInTheDocument();
  });

  it('moves focus into the dialog when opened', () => {
    renderModal();

    expect(screen.getByRole('dialog')).toContainElement(document.activeElement as HTMLElement);
  });

  it('calls onClose when Escape is pressed', async () => {
    const user = userEvent.setup();
    const { onClose } = renderModal();

    await user.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('ignores Escape when disableBackdropClose is set', async () => {
    const user = userEvent.setup();
    const { onClose } = renderModal({ disableBackdropClose: true });

    await user.keyboard('{Escape}');

    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('renders nothing when closed', () => {
    renderModal({ open: false });

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('applies the maxWidth class', () => {
    renderModal({ maxWidth: 'xs' });

    expect(screen.getByRole('dialog')).toHaveClass('max-w-[444px]');
  });
});
