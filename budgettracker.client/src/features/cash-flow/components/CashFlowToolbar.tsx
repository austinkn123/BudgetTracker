import { useMemo, useState } from 'react';
import { differenceInCalendarDays, format, startOfDay } from 'date-fns';
import { CalendarRange } from 'lucide-react';
import type { DateRange } from 'react-day-picker';
import {
  Button,
  DateRangeCalendar,
  InlineSelect,
  Modal,
  SearchInput,
  ToggleGroup,
} from '../../../shared/components/ui';
import type { CashFlowGrouping, Category, PlaidConnectionView } from '../../../shared/types/api';
import type { CashFlowControls } from '../hooks/useCashFlowControls';
import type { CashFlowRangeKey, DateSpan } from '../utils/cashFlow';

const RANGE_OPTIONS = [
  { value: 'month', label: 'This month' },
  { value: '3m', label: '3M' },
  { value: 'ytd', label: 'YTD' },
  { value: '12m', label: '12M' },
  { value: 'all', label: 'All' },
  { value: 'custom', label: 'Custom' },
] as const;

const GROUPING_OPTIONS = [
  { value: 'Day', label: 'Day' },
  { value: 'Week', label: 'Week' },
  { value: 'Month', label: 'Month' },
  { value: 'Year', label: 'Year' },
] as const;

const formatSpan = (span: DateSpan) =>
  `${format(span.from, 'MMM d')} – ${format(span.to, 'MMM d, yyyy')}`;

const dayCount = (span: DateSpan) => {
  const days = differenceInCalendarDays(span.to, span.from) + 1;
  return `${days} ${days === 1 ? 'day' : 'days'}`;
};

interface CashFlowToolbarProps {
  controls: CashFlowControls;
  connections: PlaidConnectionView[];
  categories: Category[];
}

const CashFlowToolbar = ({ controls, connections, categories }: CashFlowToolbarProps) => {
  const [pickingCustom, setPickingCustom] = useState(false);

  const accountOptions = useMemo(
    () =>
      connections.flatMap((c) =>
        c.accounts.map((a) => ({
          value: a.plaidAccountId,
          label: a.mask ? `${a.name} •••• ${a.mask}` : a.name,
        })),
      ),
    [connections],
  );

  const categoryOptions = useMemo(
    () => categories.map((c) => ({ value: c.id, label: c.name })),
    [categories],
  );

  const customSpan = controls.range === 'custom' && controls.bounds.from
    ? { from: controls.bounds.from, to: controls.bounds.to }
    : null;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <ToggleGroup<CashFlowRangeKey>
            value={controls.range}
            // Custom only takes effect once dates are applied; until then the old range stays.
            onChange={(next) => (next === 'custom' ? setPickingCustom(true) : controls.setRange(next))}
            options={RANGE_OPTIONS}
            ariaLabel="Date range"
            className="w-full rounded-full sm:w-auto [&>button]:rounded-full [&>button]:uppercase [&>button]:tracking-[0.03em]"
          />
          {customSpan && (
            <Button
              variant="secondary"
              size="sm"
              startIcon={<CalendarRange size={14} />}
              onClick={() => setPickingCustom(true)}
              aria-label={`Change custom range, currently ${formatSpan(customSpan)}`}
            >
              <span className="numeric">{formatSpan(customSpan)}</span>
            </Button>
          )}
        </div>

        <ToggleGroup<CashFlowGrouping>
          value={controls.grouping}
          onChange={controls.setGrouping}
          options={GROUPING_OPTIONS}
          ariaLabel="Group by"
        />
      </div>

      <div className="flex flex-col gap-2 sm:flex-row">
        <SearchInput
          value={controls.search}
          onChange={controls.setSearch}
          placeholder="Search payee…"
          ariaLabel="Search payee"
          className="flex-1"
        />
        <div className="sm:w-56">
          <InlineSelect<string>
            value={controls.accountId}
            onChange={controls.setAccountId}
            options={accountOptions}
            emptyOptionLabel="All accounts"
            placeholder="All accounts"
            ariaLabel="Account"
            className="h-10"
          />
        </div>
        <div className="sm:w-56">
          <InlineSelect<number>
            value={controls.categoryId}
            onChange={controls.setCategoryId}
            options={categoryOptions}
            emptyOptionLabel="All categories"
            placeholder="All categories"
            ariaLabel="Category"
            valueAs="number"
            className="h-10"
          />
        </div>
      </div>

      {pickingCustom && (
        <CustomRangeModal
          initial={customSpan}
          onApply={(span) => {
            controls.setCustomRange(span);
            setPickingCustom(false);
          }}
          onClose={() => setPickingCustom(false)}
        />
      )}
    </div>
  );
};

interface CustomRangeModalProps {
  initial: DateSpan | null;
  onApply: (span: DateSpan) => void;
  onClose: () => void;
}

/** Mounted only while open, so each opening starts from the applied range. */
const CustomRangeModal = ({ initial, onApply, onClose }: CustomRangeModalProps) => {
  const [draft, setDraft] = useState<DateRange | undefined>(initial ?? undefined);
  const today = startOfDay(new Date());
  const complete = draft?.from && draft.to ? { from: draft.from, to: draft.to } : null;

  return (
    <Modal
      open
      onClose={onClose}
      title="Custom range"
      maxWidth="xs"
      actions={
        <>
          <Button variant="ghost" onClick={onClose} className="ml-auto">
            Cancel
          </Button>
          <Button disabled={!complete} onClick={() => complete && onApply(complete)}>
            Apply
          </Button>
        </>
      }
    >
      <DateRangeCalendar
        selected={draft}
        onSelect={setDraft}
        defaultMonth={draft?.from ?? today}
        disabled={{ after: today }}
      />
      <p className="text-sm text-ink-muted" aria-live="polite">
        {complete ? (
          <span className="numeric">
            {formatSpan(complete)} · {dayCount(complete)}
          </span>
        ) : (
          'Pick a start date, then an end date.'
        )}
      </p>
    </Modal>
  );
};

export default CashFlowToolbar;
