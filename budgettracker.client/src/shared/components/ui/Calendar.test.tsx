import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { DateRange } from 'react-day-picker';
import { describe, expect, it, vi } from 'vitest';

import Calendar, { DateRangeCalendar } from './Calendar';

const MARCH = new Date(2026, 2, 1);

/** Day buttons are labelled with the full date, e.g. "Tuesday, March 10th, 2026". */
const dayButton = (day: number) =>
  screen.getByRole('button', { name: new RegExp(`March ${day}(st|nd|rd|th), 2026`) });

describe('Calendar', () => {
  it('renders the controlled month and marks the selected day', () => {
    render(
      <Calendar selected={new Date(2026, 2, 10)} onSelect={vi.fn()} month={MARCH} onMonthChange={vi.fn()} />,
    );

    expect(screen.getByText('March 2026')).toBeInTheDocument();
    expect(dayButton(10).closest('td')).toHaveAttribute('aria-selected', 'true');
    // fixedWeeks keeps the grid at six weeks even though March 2026 spans five.
    expect(screen.getAllByRole('gridcell')).toHaveLength(42);
  });

  it('calls onSelect with the clicked date', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Calendar selected={new Date(2026, 2, 10)} onSelect={onSelect} month={MARCH} onMonthChange={vi.fn()} />);

    await user.click(dayButton(15));

    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect.mock.calls[0][0]).toEqual(new Date(2026, 2, 15));
  });

  it('does not call onSelect when the already-selected day is clicked (deselect is swallowed)', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Calendar selected={new Date(2026, 2, 10)} onSelect={onSelect} month={MARCH} onMonthChange={vi.fn()} />);

    await user.click(dayButton(10));

    expect(onSelect).not.toHaveBeenCalled();
  });

  it('asks for the next month through onMonthChange', async () => {
    const user = userEvent.setup();
    const onMonthChange = vi.fn();
    render(<Calendar selected={new Date(2026, 2, 10)} onSelect={vi.fn()} month={MARCH} onMonthChange={onMonthChange} />);

    await user.click(screen.getByRole('button', { name: /next month/i }));

    expect(onMonthChange).toHaveBeenCalledWith(new Date(2026, 3, 1));
  });

  it('tints positive and negative days with their modifier classes', () => {
    render(
      <Calendar
        selected={new Date(2026, 2, 1)}
        onSelect={vi.fn()}
        month={MARCH}
        onMonthChange={vi.fn()}
        positiveDays={[new Date(2026, 2, 5)]}
        negativeDays={[new Date(2026, 2, 6)]}
      />,
    );

    expect(dayButton(5).closest('td')).toHaveClass('day-pos');
    expect(dayButton(6).closest('td')).toHaveClass('day-neg');
    expect(dayButton(7).closest('td')).not.toHaveClass('day-pos');
  });
});

describe('DateRangeCalendar', () => {
  const Harness = ({ onSelect }: { onSelect: (r: DateRange | undefined) => void }) => {
    const [range, setRange] = useState<DateRange | undefined>();
    return (
      <DateRangeCalendar
        selected={range}
        onSelect={(next) => {
          setRange(next);
          onSelect(next);
        }}
        defaultMonth={MARCH}
        disabled={{ after: new Date(2026, 2, 20) }}
      />
    );
  };

  it('selects a from-to range over two clicks', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Harness onSelect={onSelect} />);

    await user.click(dayButton(3));
    await user.click(dayButton(8));

    expect(onSelect.mock.lastCall?.[0]).toEqual({ from: new Date(2026, 2, 3), to: new Date(2026, 2, 8) });
  });

  it('disables days matching the disabled matcher', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Harness onSelect={onSelect} />);

    expect(dayButton(25)).toBeDisabled();
    await user.click(dayButton(25));
    expect(onSelect).not.toHaveBeenCalled();
  });
});
