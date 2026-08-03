import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import { alpha, useTheme } from '@mui/material/styles';
import { formatDistanceToNow, parseISO } from 'date-fns';
import { useMemo } from 'react';
import type { Category, Transaction } from '../../../shared/types/api';
import { categoryLabel } from '../utils/selectors';
import { getChartPalette, getSemanticColors } from '../utils/chartTheme';

interface RecentActivityFeedProps {
  /** Window-scoped transactions, already sorted newest-first and capped. */
  items: Transaction[];
  /** Full category list — drives names and the stable palette-by-index chip colors. */
  categories: Category[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
});

const RecentActivityFeed = ({ items, categories }: RecentActivityFeedProps) => {
  const theme = useTheme();
  const semantic = getSemanticColors(theme);
  const palette = getChartPalette(theme);

  const categoryNames = useMemo(
    () => new Map(categories.map((c) => [c.id, c.name])),
    [categories],
  );

  // Stable color per category — index into chart palette by category position.
  const colorByCategory = useMemo(() => {
    const map = new Map<number, string>();
    categories.forEach((c, idx) => {
      map.set(c.id, palette[idx % palette.length]);
    });
    return map;
  }, [categories, palette]);

  return (
    <Card className="h-full">
      <CardContent>
        <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
          Recent Activity
        </Typography>
        {items.length === 0 ? (
          <Box className="flex items-center justify-center" sx={{ minHeight: 160 }}>
            <Typography variant="body2" color="text.secondary">
              No transactions yet
            </Typography>
          </Box>
        ) : (
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.25 }}>
            {items.map((t) => {
              const color =
                (t.categoryId != null ? colorByCategory.get(t.categoryId) : undefined) ??
                semantic.neutral;
              const name = categoryLabel(categoryNames, t.categoryId ?? null);
              const isIncome = t.transactionType === 'Income';
              return (
                <Box
                  key={t.id}
                  sx={{
                    display: 'grid',
                    gridTemplateColumns: 'auto 1fr auto',
                    alignItems: 'center',
                    gap: 1.5,
                    py: 0.5,
                  }}
                >
                  <Chip
                    label={name}
                    size="small"
                    sx={{
                      backgroundColor: alpha(color, 0.18),
                      color: semantic.ink,
                      fontWeight: 600,
                      borderRadius: 999,
                      maxWidth: 140,
                    }}
                  />
                  <Box sx={{ minWidth: 0 }}>
                    <Typography
                      variant="body2"
                      sx={{ fontWeight: 600, color: semantic.ink }}
                      noWrap
                    >
                      {t.payee || name}
                    </Typography>
                    <Typography variant="caption" sx={{ color: semantic.expense }}>
                      {formatDistanceToNow(parseISO(t.occurredAt), { addSuffix: true })}
                    </Typography>
                  </Box>
                  <Typography
                    variant="body2"
                    sx={{
                      fontWeight: 700,
                      color: isIncome ? semantic.income : semantic.expense,
                      whiteSpace: 'nowrap',
                    }}
                  >
                    {isIncome ? '+' : '-'}
                    {currency.format(Math.abs(t.amount))}
                  </Typography>
                </Box>
              );
            })}
          </Box>
        )}
      </CardContent>
    </Card>
  );
};

export default RecentActivityFeed;
