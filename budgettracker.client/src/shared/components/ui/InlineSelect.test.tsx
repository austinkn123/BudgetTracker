import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import InlineSelect from './InlineSelect';

// Radix Select relies on pointer-capture and scrollIntoView, which jsdom does not implement.
beforeAll(() => {
  Element.prototype.hasPointerCapture ??= () => false;
  Element.prototype.releasePointerCapture ??= () => undefined;
  Element.prototype.scrollIntoView ??= () => undefined;
});

const NUMERIC_OPTIONS = [
  { value: 1, label: 'Groceries' },
  { value: 2, label: 'Rent' },
  { value: 3, label: 'Archived', disabled: true },
] as const;

const trigger = () => screen.getByRole('combobox', { name: 'Category' });

describe('InlineSelect', () => {
  it('emits the typed number when an option is chosen with valueAs="number"', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <InlineSelect<number>
        value={1}
        onChange={onChange}
        options={NUMERIC_OPTIONS}
        ariaLabel="Category"
        valueAs="number"
      />,
    );
    expect(trigger()).toHaveTextContent('Groceries');

    await user.click(trigger());
    await user.click(await screen.findByRole('option', { name: 'Rent' }));

    expect(onChange).toHaveBeenCalledExactlyOnceWith(2);
  });

  it('keeps string values as strings by default', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <InlineSelect
        value="a"
        onChange={onChange}
        options={[
          { value: 'a', label: 'Alpha' },
          { value: 'b', label: 'Beta' },
        ]}
        ariaLabel="Category"
      />,
    );

    await user.click(trigger());
    await user.click(await screen.findByRole('option', { name: 'Beta' }));

    expect(onChange).toHaveBeenCalledExactlyOnceWith('b');
  });

  it('emits null when the empty option is chosen, and shows its label when value is null', async () => {
    const user = userEvent.setup();
    const Harness = () => {
      const [value, setValue] = useState<number | null>(2);
      return (
        <>
          <InlineSelect<number>
            value={value}
            onChange={setValue}
            options={NUMERIC_OPTIONS}
            emptyOptionLabel="Uncategorized"
            ariaLabel="Category"
            valueAs="number"
          />
          <output>{value === null ? 'null' : String(value)}</output>
        </>
      );
    };
    render(<Harness />);

    await user.click(trigger());
    await user.click(await screen.findByRole('option', { name: 'Uncategorized' }));

    expect(screen.getByRole('status')).toHaveTextContent('null');
    expect(trigger()).toHaveTextContent('Uncategorized');
  });

  it('does not select a disabled option', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <InlineSelect<number>
        value={1}
        onChange={onChange}
        options={NUMERIC_OPTIONS}
        ariaLabel="Category"
        valueAs="number"
      />,
    );

    await user.click(trigger());
    const archived = await screen.findByRole('option', { name: 'Archived' });
    expect(archived).toHaveAttribute('aria-disabled', 'true');
    await user.click(archived);

    expect(onChange).not.toHaveBeenCalled();
  });

  it('does not open when disabled', async () => {
    const user = userEvent.setup();
    render(
      <InlineSelect value={null} onChange={vi.fn()} options={NUMERIC_OPTIONS} ariaLabel="Category" disabled />,
    );

    expect(trigger()).toBeDisabled();
    await user.click(trigger());

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  // Regression: a null value used to map to the '__empty__' sentinel, so Radix never treated
  // the select as unset and the placeholder was hidden when there is no emptyOptionLabel.
  it('shows the placeholder when value is null and there is no empty option', () => {
    render(
      <InlineSelect
        value={null}
        onChange={vi.fn()}
        options={NUMERIC_OPTIONS}
        placeholder="Pick a category"
        ariaLabel="Category"
      />,
    );

    expect(trigger()).toHaveTextContent('Pick a category');
    expect(trigger()).toHaveAttribute('data-placeholder');
  });

  // An action picker applies its choice elsewhere and holds no value of its own, so it must keep
  // showing its placeholder and fire for every option — including the empty one, which a stateful
  // select treats as "already selected" when its value is null and so never reports.
  describe('mode="action"', () => {
    const renderAction = (onChange = vi.fn()) => {
      render(
        <InlineSelect<number>
          mode="action"
          onChange={onChange}
          options={NUMERIC_OPTIONS}
          emptyOptionLabel="Uncategorized"
          placeholder="Set category…"
          ariaLabel="Category"
          valueAs="number"
        />,
      );
      return onChange;
    };

    it('shows the placeholder rather than the empty option label', () => {
      renderAction();

      expect(trigger()).toHaveTextContent('Set category…');
      expect(trigger()).toHaveAttribute('data-placeholder');
    });

    it('emits null when the empty option is picked', async () => {
      const user = userEvent.setup();
      const onChange = renderAction();

      await user.click(trigger());
      await user.click(await screen.findByRole('option', { name: 'Uncategorized' }));

      expect(onChange).toHaveBeenCalledExactlyOnceWith(null);
    });

    it('emits the typed id for an option and resets to the placeholder', async () => {
      const user = userEvent.setup();
      const onChange = renderAction();

      await user.click(trigger());
      await user.click(await screen.findByRole('option', { name: 'Rent' }));

      expect(onChange).toHaveBeenCalledExactlyOnceWith(2);
      expect(trigger()).toHaveTextContent('Set category…');
    });

    it('fires again when the same option is picked twice', async () => {
      const user = userEvent.setup();
      const onChange = renderAction();

      for (let i = 0; i < 2; i += 1) {
        await user.click(trigger());
        await user.click(await screen.findByRole('option', { name: 'Groceries' }));
      }

      expect(onChange).toHaveBeenNthCalledWith(1, 1);
      expect(onChange).toHaveBeenNthCalledWith(2, 1);
    });
  });
});
