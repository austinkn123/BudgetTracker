import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import { alpha, useTheme } from '@mui/material/styles';
import { useMemo } from 'react';
import type { Category, Transaction } from '../../../shared/types/api';
import { aggregateByCategory } from '../utils/chartHelpers';
import { getChartPalette, getSemanticColors } from '../utils/chartTheme';

interface WhereItWentProps {
  /** Transactions already filtered to the active range. */
  transactions: Transaction[];
  categories: Category[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

const TOP_N = 8;

export function WhereItWent({ transactions, categories }: WhereItWentProps) {
  const theme = useTheme();
  const palette = getChartPalette(theme);
  const semantic = getSemanticColors(theme);

  const rows = useMemo(() => {
    const slices = aggregateByCategory(transactions, categories);
    const top = slices.slice(0, TOP_N);
    const rest = slices.slice(TOP_N);
    const otherTotal = rest.reduce((sum, s) => sum + s.value, 0);
    const combined = [...top];
    if (otherTotal > 0) {
      combined.push({ id: -1, label: 'Other', value: otherTotal });
    }
    return combined;
  }, [transactions, categories]);

  const max = rows.reduce((m, r) => Math.max(m, r.value), 0);

  return (
    <Card className="h-full">
      <CardContent>
        <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
          Where It Went
        </Typography>
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
              const color = row.id === -1 ? semantic.neutral : palette[idx % palette.length];
              return (
                <Box key={row.id}>
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
      </CardContent>
    </Card>
  );
}
