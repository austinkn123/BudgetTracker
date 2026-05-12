import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import type { DateRangeKey } from '../hooks/useDateRange';

interface RangeSelectorProps {
  value: DateRangeKey;
  onChange: (next: DateRangeKey) => void;
}

const OPTIONS: Array<{ value: DateRangeKey; label: string }> = [
  { value: 'month', label: 'This Month' },
  { value: '3m', label: '3M' },
  { value: 'ytd', label: 'YTD' },
  { value: 'all', label: 'All' },
];

export function RangeSelector({ value, onChange }: RangeSelectorProps) {
  return (
    <ToggleButtonGroup
      size="small"
      exclusive
      value={value}
      onChange={(_event, next: DateRangeKey | null) => {
        if (next) onChange(next);
      }}
      aria-label="Dashboard date range"
    >
      {OPTIONS.map((opt) => (
        <ToggleButton key={opt.value} value={opt.value} aria-label={opt.label}>
          {opt.label}
        </ToggleButton>
      ))}
    </ToggleButtonGroup>
  );
}
