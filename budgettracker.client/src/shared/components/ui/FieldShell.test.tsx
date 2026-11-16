import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import FieldShell from './FieldShell';

describe('FieldShell', () => {
  it('labels its control through htmlFor and shows helper text', () => {
    render(
      <FieldShell label="Amount" htmlFor="amount" helperText="In dollars">
        <input id="amount" />
      </FieldShell>,
    );

    expect(screen.getByLabelText('Amount')).toHaveAttribute('id', 'amount');
    expect(screen.getByText('In dollars')).toHaveClass('text-ink-muted');
  });

  it('marks required fields with a decorative asterisk that stays out of the accessible name', () => {
    render(
      <FieldShell label="Amount" htmlFor="amount" required>
        <input id="amount" />
      </FieldShell>,
    );

    expect(screen.getByText('*')).toHaveAttribute('aria-hidden');
    expect(screen.getByRole('textbox')).toHaveAccessibleName('Amount');
  });

  it('shows the error in place of the helper text, in the error token', () => {
    render(
      <FieldShell label="Amount" htmlFor="amount" helperText="In dollars" error="Required">
        <input id="amount" />
      </FieldShell>,
    );

    expect(screen.getByText('Required')).toHaveClass('text-error');
    expect(screen.queryByText('In dollars')).not.toBeInTheDocument();
  });

  it('renders no helper slot when there is neither error nor helper text', () => {
    const { container } = render(
      <FieldShell label="Amount" htmlFor="amount">
        <input id="amount" />
      </FieldShell>,
    );

    expect(container.querySelector('p')).toBeNull();
  });

  it('gives the helper/error message the messageId so a control can reference it', () => {
    const { rerender } = render(
      <FieldShell label="Amount" htmlFor="amount" messageId="amount-msg" helperText="In dollars">
        <input id="amount" aria-describedby="amount-msg" />
      </FieldShell>,
    );
    expect(screen.getByRole('textbox')).toHaveAccessibleDescription('In dollars');

    rerender(
      <FieldShell label="Amount" htmlFor="amount" messageId="amount-msg" helperText="In dollars" error="Required">
        <input id="amount" aria-describedby="amount-msg" />
      </FieldShell>,
    );
    expect(screen.getByRole('textbox')).toHaveAccessibleDescription('Required');
  });
});
