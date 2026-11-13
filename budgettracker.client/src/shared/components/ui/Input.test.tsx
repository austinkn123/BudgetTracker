import { zodResolver } from '@hookform/resolvers/zod';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useForm } from 'react-hook-form';
import { describe, expect, it, vi } from 'vitest';
import { z } from 'zod';

import Input from './Input';
import type { InputProps } from './Input';

const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  amount: z.number({ error: 'Enter an amount' }).positive('Must be positive').optional(),
});

type FormValues = z.infer<typeof schema>;

type HarnessProps = {
  onSubmit?: (values: FormValues) => void;
  defaultValues?: Partial<FormValues>;
} & Partial<Pick<InputProps<FormValues>, 'helperText' | 'required' | 'disabled' | 'multiline'>>;

const Harness = ({ onSubmit = vi.fn(), defaultValues = { name: '' }, ...nameProps }: HarnessProps) => {
  const { control, handleSubmit } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues,
  });
  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate>
      <Input control={control} name="name" label="Name" {...nameProps} />
      <Input control={control} name="amount" label="Amount" type="number" valueAs="number" />
      <button type="submit">Submit</button>
    </form>
  );
};

describe('Input', () => {
  it('renders a labelled field with its helper text and the form value', () => {
    render(<Harness defaultValues={{ name: 'Rent' }} helperText="Shown on the ledger" />);

    expect(screen.getByRole('textbox', { name: 'Name' })).toHaveValue('Rent');
    expect(screen.getByText('Shown on the ledger')).toBeInTheDocument();
  });

  it('renders the zod error in the helper slot on submit, replacing the hint', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<Harness onSubmit={onSubmit} helperText="Shown on the ledger" />);

    await user.click(screen.getByRole('button', { name: 'Submit' }));

    const field = screen.getByRole('textbox', { name: 'Name' });
    expect(await screen.findByText('Name is required')).toHaveClass('text-error');
    expect(screen.queryByText('Shown on the ledger')).not.toBeInTheDocument();
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(field).toHaveClass('border-error');
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('clears the error once the value becomes valid', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('button', { name: 'Submit' }));
    expect(await screen.findByText('Name is required')).toBeInTheDocument();

    await user.type(screen.getByRole('textbox', { name: 'Name' }), 'Rent');

    expect(screen.queryByText('Name is required')).not.toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Name' })).not.toHaveAttribute('aria-invalid');
  });

  it('coerces valueAs="number" and leaves an empty number field undefined', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<Harness onSubmit={onSubmit} defaultValues={{ name: 'Rent' }} />);

    await user.click(screen.getByRole('button', { name: 'Submit' }));
    expect(onSubmit.mock.calls[0][0]).toEqual({ name: 'Rent', amount: undefined });

    await user.type(screen.getByRole('spinbutton', { name: 'Amount' }), '12.5');
    await user.click(screen.getByRole('button', { name: 'Submit' }));
    expect(onSubmit.mock.calls[1][0]).toEqual({ name: 'Rent', amount: 12.5 });
  });

  it('shows a number-field schema error', async () => {
    const user = userEvent.setup();
    render(<Harness defaultValues={{ name: 'Rent' }} />);

    await user.type(screen.getByRole('spinbutton', { name: 'Amount' }), '-3');
    await user.click(screen.getByRole('button', { name: 'Submit' }));

    expect(await screen.findByText('Must be positive')).toBeInTheDocument();
  });

  it('renders a textarea when multiline and honours disabled', () => {
    render(<Harness multiline disabled />);

    const field = screen.getByRole('textbox', { name: 'Name' });
    expect(field.tagName).toBe('TEXTAREA');
    expect(field).toBeDisabled();
  });
});
