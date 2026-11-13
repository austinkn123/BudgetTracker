import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import SearchInput from './SearchInput';

const Harness = ({ onChange }: { onChange?: (value: string) => void }) => {
  const [value, setValue] = useState('');
  return (
    <SearchInput
      value={value}
      onChange={(next) => {
        onChange?.(next);
        setValue(next);
      }}
      placeholder="Search transactions"
      ariaLabel="Search transactions"
    />
  );
};

const box = () => screen.getByRole('searchbox', { name: 'Search transactions' });
const clear = () => screen.queryByRole('button', { name: 'Clear search' });

describe('SearchInput', () => {
  it('is a labelled searchbox that reports each keystroke', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);

    await user.type(box(), 'tea');

    expect(box()).toHaveValue('tea');
    expect(onChange).toHaveBeenLastCalledWith('tea');
    expect(onChange).toHaveBeenCalledTimes(3);
  });

  it('hides the clear button while empty', () => {
    render(<Harness />);

    expect(box()).toHaveAttribute('placeholder', 'Search transactions');
    expect(clear()).not.toBeInTheDocument();
  });

  it('clears the value with the clear button, which then disappears', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    await user.type(box(), 'rent');

    await user.click(clear()!);

    expect(onChange).toHaveBeenLastCalledWith('');
    expect(box()).toHaveValue('');
    expect(clear()).not.toBeInTheDocument();
  });
});
