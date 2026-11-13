import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useForm } from 'react-hook-form';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import Select from './Select';
import type { SelectOption } from './Select';

// Radix Select relies on pointer-capture and scrollIntoView, which jsdom does not implement.
beforeAll(() => {
  Element.prototype.hasPointerCapture ??= () => false;
  Element.prototype.releasePointerCapture ??= () => undefined;
  Element.prototype.scrollIntoView ??= () => undefined;
});

interface FormValues {
  categoryId: number | null | undefined;
}

const OPTIONS: readonly SelectOption<number>[] = [
  { value: 10, label: 'Groceries' },
  { value: 20, label: 'Rent' },
];

interface HarnessProps {
  defaultValue: number | null;
  onValue: (value: FormValues['categoryId']) => void;
  emptyOptionLabel?: string;
  error?: string;
  disabled?: boolean;
}

/** Owns a real react-hook-form instance and reports every value change. */
const Harness = ({ defaultValue, onValue, emptyOptionLabel, error, disabled }: HarnessProps) => {
  const { control, watch, setError } = useForm<FormValues>({
    defaultValues: { categoryId: defaultValue },
  });
  const value = watch('categoryId');
  onValue(value);
  return (
    <>
      <Select<FormValues, number>
        control={control}
        name="categoryId"
        label="Category"
        options={OPTIONS}
        valueAs="number"
        emptyOptionLabel={emptyOptionLabel}
        placeholder="Pick a category"
        helperText="Where this spend goes"
        disabled={disabled}
        required
      />
      {error && (
        <button type="button" onClick={() => setError('categoryId', { message: error })}>
          raise error
        </button>
      )}
    </>
  );
};

const trigger = () => screen.getByRole('combobox', { name: /Category/ });

describe('Select', () => {
  it('labels the trigger and writes the chosen option to the form as a number', async () => {
    const user = userEvent.setup();
    const onValue = vi.fn();
    render(<Harness defaultValue={10} onValue={onValue} />);
    expect(trigger()).toHaveTextContent('Groceries');
    expect(screen.getByText('Where this spend goes')).toBeInTheDocument();

    await user.click(trigger());
    await user.click(await screen.findByRole('option', { name: 'Rent' }));

    expect(onValue).toHaveBeenLastCalledWith(20);
    expect(trigger()).toHaveTextContent('Rent');
  });

  it('clears the form value to undefined when the empty option is chosen', async () => {
    const user = userEvent.setup();
    const onValue = vi.fn();
    render(<Harness defaultValue={20} onValue={onValue} emptyOptionLabel="Uncategorized" />);

    await user.click(trigger());
    await user.click(await screen.findByRole('option', { name: 'Uncategorized' }));

    expect(onValue).toHaveBeenLastCalledWith(undefined);
  });

  // BUG (reported, not fixed here): the empty option writes `undefined`, and RHF's Controller
  // falls back to the field's defaultValue for an undefined value, so the trigger snaps back to
  // the default option ("Rent") instead of showing "Uncategorized". Writing `null` would fix it.
  it.fails('shows the empty option label after it is chosen over a non-empty default', async () => {
    const user = userEvent.setup();
    render(<Harness defaultValue={20} onValue={vi.fn()} emptyOptionLabel="Uncategorized" />);

    await user.click(trigger());
    await user.click(await screen.findByRole('option', { name: 'Uncategorized' }));

    expect(trigger()).toHaveTextContent('Uncategorized');
  });

  it('marks the trigger invalid and shows the error in place of the helper text', async () => {
    const user = userEvent.setup();
    render(<Harness defaultValue={10} onValue={vi.fn()} error="Category is required" />);
    expect(trigger()).not.toHaveAttribute('aria-invalid');

    await user.click(screen.getByRole('button', { name: 'raise error' }));

    expect(trigger()).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Category is required')).toBeInTheDocument();
    expect(screen.queryByText('Where this spend goes')).not.toBeInTheDocument();
  });

  it('does not open when disabled', async () => {
    const user = userEvent.setup();
    render(<Harness defaultValue={10} onValue={vi.fn()} disabled />);

    expect(trigger()).toBeDisabled();
    await user.click(trigger());

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  // BUG (reported, not fixed here): a null value maps to the '__empty__' sentinel rather than
  // '', so Radix never treats the select as unset and the placeholder is not rendered when
  // there is no emptyOptionLabel. `it.fails` keeps the suite green; flip to `it` once fixed.
  it.fails('shows the placeholder when the form value is null', () => {
    render(<Harness defaultValue={null} onValue={vi.fn()} />);

    expect(trigger()).toHaveTextContent('Pick a category');
  });
});
