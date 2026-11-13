import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useForm } from 'react-hook-form';
import { describe, expect, it, vi } from 'vitest';

import Checkbox from './Checkbox';

// Inside a <form>, Radix Checkbox renders a hidden bubble input sized via ResizeObserver, which jsdom lacks.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
};

interface FormValues {
  recurring: boolean;
}

const Harness = ({
  defaultValue = false,
  disabled = false,
  onSubmit,
}: {
  defaultValue?: boolean;
  disabled?: boolean;
  onSubmit: (values: FormValues) => void;
}) => {
  const { control, handleSubmit } = useForm<FormValues>({ defaultValues: { recurring: defaultValue } });
  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <Checkbox control={control} name="recurring" label="Recurring" disabled={disabled} />
      <button type="submit">Submit</button>
    </form>
  );
};

describe('Checkbox', () => {
  it('renders a labelled checkbox reflecting the form value', () => {
    render(<Harness defaultValue onSubmit={vi.fn()} />);

    expect(screen.getByRole('checkbox', { name: 'Recurring' })).toBeChecked();
  });

  it('toggles via its label and submits a boolean', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<Harness onSubmit={onSubmit} />);

    const checkbox = screen.getByRole('checkbox', { name: 'Recurring' });
    expect(checkbox).not.toBeChecked();
    await user.click(screen.getByText('Recurring'));
    expect(checkbox).toBeChecked();

    await user.click(screen.getByRole('button', { name: 'Submit' }));
    expect(onSubmit.mock.calls[0][0]).toEqual({ recurring: true });
  });

  it('does not toggle when disabled', async () => {
    const user = userEvent.setup();
    render(<Harness disabled onSubmit={vi.fn()} />);

    const checkbox = screen.getByRole('checkbox', { name: 'Recurring' });
    expect(checkbox).toBeDisabled();
    await user.click(checkbox);
    expect(checkbox).not.toBeChecked();
  });
});
