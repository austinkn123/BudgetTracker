import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import InlineCheckbox from './InlineCheckbox';

describe('InlineCheckbox', () => {
  it('renders a checkbox named by ariaLabel and reports the toggled value', async () => {
    const user = userEvent.setup();
    const onCheckedChange = vi.fn();
    render(<InlineCheckbox checked={false} onCheckedChange={onCheckedChange} ariaLabel="Select row" />);

    const checkbox = screen.getByRole('checkbox', { name: 'Select row' });
    expect(checkbox).not.toBeChecked();
    await user.click(checkbox);

    expect(onCheckedChange).toHaveBeenCalledWith(true);
  });

  it('toggles with the keyboard', async () => {
    const user = userEvent.setup();
    const Harness = () => {
      const [checked, setChecked] = useState(false);
      return <InlineCheckbox checked={checked} onCheckedChange={setChecked} ariaLabel="Select row" />;
    };
    render(<Harness />);

    await user.tab();
    await user.keyboard(' ');

    expect(screen.getByRole('checkbox', { name: 'Select row' })).toBeChecked();
  });

  it('exposes the indeterminate state as aria-checked="mixed" and resolves to true on click', async () => {
    const user = userEvent.setup();
    const onCheckedChange = vi.fn();
    render(<InlineCheckbox checked="indeterminate" onCheckedChange={onCheckedChange} ariaLabel="Select all" />);

    const checkbox = screen.getByRole('checkbox', { name: 'Select all' });
    expect(checkbox).toHaveAttribute('aria-checked', 'mixed');
    expect(checkbox).toHaveAttribute('data-state', 'indeterminate');
    await user.click(checkbox);

    expect(onCheckedChange).toHaveBeenCalledWith(true);
  });

  it('ignores clicks when disabled', async () => {
    const user = userEvent.setup();
    const onCheckedChange = vi.fn();
    render(<InlineCheckbox checked={false} onCheckedChange={onCheckedChange} ariaLabel="Select row" disabled />);

    const checkbox = screen.getByRole('checkbox', { name: 'Select row' });
    expect(checkbox).toBeDisabled();
    await user.click(checkbox);

    expect(onCheckedChange).not.toHaveBeenCalled();
  });
});
