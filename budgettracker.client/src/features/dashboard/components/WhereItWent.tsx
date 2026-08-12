import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import { alpha, useTheme } from '@mui/material/styles';
import BudCard from '../../../shared/components/ui/BudCard';
import type { SpendSlice } from '../utils/selectors';
import { getChartPalette, getSemanticColors } from '../utils/chartTheme';

interface WhereItWentProps {
  /** Window-scoped expense totals, already ranked with a trailing "Other". */
  rows: SpendSlice[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

const WhereItWent = ({ rows }: WhereItWentProps) => {
  const theme = useTheme();
  const palette = getChartPalette(theme);
  const semantic = getSemanticColors(theme);

  const max = rows.reduce((m, r) => Math.max(m, r.value), 0);

  return (
    <BudCard title="Where It Went" fullHeight>
      <>
        {rows.length === 0 ? (
          <Box className="flex items-center justify-center" sx={{ minHeight: 240 }}>
            <Typography variant="body2" color="text.secondary">
              No expenses in this range
            </Typography>
          </Box>
        ) : (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
            {rows.map((row, idx) => {
              const pct = max > 0 ? (row.value / max) * 100 : 0;
              const color = row.key === 'other' ? semantic.neutral : palette[idx % palette.length];
              return (
                <Box key={row.key}>
                  <Box className="flex items-baseline justify-between">
                    <Typography
                      variant="body2"
                      sx={{ fontWeight: 600, color: semantic.ink }}
                    >
                      {row.label}
                    </Typography>
                    <Typography variant="caption" sx={{ color: semantic.expense }}>
                      {currency.format(row.value)}
                    </Typography>
                  </Box>
                  <Box
                    sx={{
                      mt: 0.5,
                      height: 10,
                      borderRadius: 5,
                      backgroundColor: alpha(semantic.neutral, 0.3),
                      overflow: 'hidden',
                    }}
                  >
                    <Box
                      sx={{
                        width: `${pct}%`,
                        height: '100%',
                        backgroundColor: color,
                        borderRadius: 5,
                        transition: 'width 240ms ease',
                      }}
                    />
                  </Box>
                </Box>
              );
            })}
          </Box>
        )}
      </>
    </BudCard>
  );
};

export default WhereItWent;
