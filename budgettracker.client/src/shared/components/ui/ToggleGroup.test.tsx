import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import ToggleGroup from './ToggleGroup';

const OPTIONS = [
  { value: 'month', label: 'Month' },
  { value: 'quarter', label: 'Quarter' },
  { value: 'year', label: 'Year' },
] as const;

type Range = (typeof OPTIONS)[number]['value'];

/** Owns the selection, as a page does. */
const Harness = ({ onChange }: { onChange?: (value: Range) => void }) => {
  const [value, setValue] = useState<Range>('month');
  return (
    <ToggleGroup<Range>
      value={value}
      onChange={(next) => {
        onChange?.(next);
        setValue(next);
      }}
      options={OPTIONS}
      ariaLabel="Range"
    />
  );
};

const item = (name: string) => screen.getByRole('radio', { name });

describe('ToggleGroup', () => {
  it('renders a labelled single-select group with the current value checked', () => {
    render(<Harness />);

    expect(screen.getByRole('radiogroup', { name: 'Range' })).toBeInTheDocument();
    expect(item('Month')).toHaveAttribute('aria-checked', 'true');
    expect(item('Quarter')).toHaveAttribute('aria-checked', 'false');
  });

  it('emits the typed value and moves the selection on click', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);

    await user.click(item('Year'));

    expect(onChange).toHaveBeenCalledExactlyOnceWith('year');
    expect(item('Year')).toHaveAttribute('aria-checked', 'true');
    expect(item('Month')).toHaveAttribute('aria-checked', 'false');
  });

  it('keeps the selection when the active item is clicked again', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);

    await user.click(item('Month'));

    expect(onChange).not.toHaveBeenCalled();
    expect(item('Month')).toHaveAttribute('aria-checked', 'true');
  });

  it('moves focus with the arrow keys and selects with Space', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    await user.tab();
    expect(item('Month')).toHaveFocus();

    await user.keyboard('{ArrowRight}');
    expect(item('Quarter')).toHaveFocus();
    await user.keyboard(' ');

    expect(onChange).toHaveBeenCalledExactlyOnceWith('quarter');
  });
});
