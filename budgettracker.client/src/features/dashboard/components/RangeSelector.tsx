import { alpha } from '@mui/material/styles';
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
      sx={{
        width: { xs: '100%', sm: 'auto' },
        p: 0.5,
        gap: 0.5,
        border: (theme) => `1px solid ${theme.palette.divider}`,
        borderRadius: 999,
        backgroundColor: (theme) => alpha(theme.palette.background.paper, 0.88),
        boxShadow: '0 10px 30px -24px rgba(15, 23, 42, 0.45)',
        backdropFilter: 'blur(10px)',
        '& .MuiToggleButtonGroup-grouped': {
          flex: { xs: 1, sm: 'initial' },
          border: 0,
          borderRadius: '999px !important',
          px: 1.75,
          py: 0.75,
          minWidth: 0,
          color: 'text.secondary',
          fontSize: '0.8125rem',
          fontWeight: 700,
          letterSpacing: '0.03em',
          textTransform: 'uppercase',
          transition: 'background-color 160ms ease, color 160ms ease, box-shadow 160ms ease',
          '&:hover': {
            backgroundColor: (theme) => alpha(theme.palette.primary.main, 0.08),
          },
          '&.Mui-selected': {
            backgroundColor: 'background.paper',
            color: 'text.primary',
            boxShadow: '0 8px 18px -16px rgba(15, 23, 42, 0.9)',
          },
          '&.Mui-selected:hover': {
            backgroundColor: 'background.paper',
          },
          '&.Mui-focusVisible': {
            outline: (theme) => `2px solid ${theme.palette.primary.main}`,
            outlineOffset: 2,
          },
        },
      }}
    >
      {OPTIONS.map((opt) => (
        <ToggleButton key={opt.value} value={opt.value} aria-label={opt.label}>
          {opt.label}
        </ToggleButton>
      ))}
    </ToggleButtonGroup>
  );
}
