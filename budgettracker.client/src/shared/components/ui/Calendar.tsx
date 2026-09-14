import { DayPicker } from 'react-day-picker';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { cn } from '../../utils/cn';

export interface CalendarProps {
  selected: Date;
  onSelect: (date: Date) => void;
  /** Controlled visible month — required so onMonthChange fires reliably (RDP v9). */
  month: Date;
  onMonthChange: (month: Date) => void;
  /**
   * Days to tint green/red (net positive/negative). Callers must pre-filter
   * to the visible month — RDP applies modifiers to outside days too.
   */
  positiveDays?: Date[];
  negativeDays?: Date[];
  className?: string;
}

/**
 * Month calendar (BUD-20). Replaces @mui/x-date-pickers DateCalendar.
 * Styled entirely through the classNames API — react-day-picker's stylesheet
 * is deliberately NOT imported, so there is no specificity fight to manage.
 * `fixedWeeks` keeps the grid at 6 rows so panel height never jumps.
 */
const Calendar = ({
  selected,
  onSelect,
  month,
  onMonthChange,
  positiveDays = [],
  negativeDays = [],
  className,
}: CalendarProps) => (
  <DayPicker
    mode="single"
    required={false}
    selected={selected}
    onSelect={(date) => {
      if (date) onSelect(date);
    }}
    month={month}
    onMonthChange={onMonthChange}
    showOutsideDays
    fixedWeeks
    modifiers={{ positive: positiveDays, negative: negativeDays }}
    modifiersClassNames={{ positive: 'day-pos', negative: 'day-neg' }}
    components={{
      Chevron: ({ orientation }) =>
        orientation === 'left' ? <ChevronLeft size={18} /> : <ChevronRight size={18} />,
    }}
    className={cn('w-full select-none', className)}
    classNames={{
      months: 'relative flex flex-col',
      month: 'w-full',
      month_caption: 'flex h-9 items-center justify-center',
      caption_label: 'text-sm font-semibold text-ink',
      nav: 'absolute inset-x-0 top-0 z-10 flex h-9 items-center justify-between',
      button_previous:
        'focus-ring inline-flex h-8 w-8 items-center justify-center rounded text-ink-muted transition-colors duration-120 hover:bg-border-subtle hover:text-ink',
      button_next:
        'focus-ring inline-flex h-8 w-8 items-center justify-center rounded text-ink-muted transition-colors duration-120 hover:bg-border-subtle hover:text-ink',
      month_grid: 'mt-3 w-full border-separate border-spacing-1',
      weekdays: '',
      weekday: 'pb-1 text-center text-xs font-semibold uppercase tracking-[0.04em] text-ink-muted',
      week: '',
      day: 'rounded p-0 text-center',
      day_button:
        'focus-ring mx-auto flex h-9 w-full min-w-9 cursor-pointer items-center justify-center rounded text-sm text-ink transition-colors duration-120 hover:bg-border-subtle',
      selected: 'bg-primary text-white [&_button]:text-white [&_button:hover]:bg-transparent',
      today: 'font-bold',
      outside: 'opacity-40',
      hidden: 'invisible',
    }}
  />
);

export default Calendar;
