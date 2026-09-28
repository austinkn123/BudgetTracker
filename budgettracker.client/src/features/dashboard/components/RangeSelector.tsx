import { ToggleGroup } from '../../../shared/components/ui';
import type { DateRangeKey } from '../hooks/useDateRange';

interface RangeSelectorProps {
  value: DateRangeKey;
  onChange: (next: DateRangeKey) => void;
}

const OPTIONS = [
  { value: 'month', label: 'This Month' },
  { value: '3m', label: '3M' },
  { value: 'ytd', label: 'YTD' },
  { value: 'all', label: 'All' },
] as const;

const RangeSelector = ({ value, onChange }: RangeSelectorProps) => (
  <ToggleGroup<DateRangeKey>
    value={value}
    onChange={onChange}
    options={OPTIONS}
    ariaLabel="Dashboard date range"
    className="w-full rounded-full sm:w-auto [&>button]:rounded-full [&>button]:uppercase [&>button]:tracking-[0.03em]"
  />
);

export default RangeSelector;
