import Card from '@mui/material/Card';
import CardActionArea from '@mui/material/CardActionArea';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import Collapse from '@mui/material/Collapse';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Divider from '@mui/material/Divider';
import { SparkLineChart } from '@mui/x-charts/SparkLineChart';
import { alpha, useTheme } from '@mui/material/styles';
import { format, parseISO } from 'date-fns';
import type { Transaction } from '../../../shared/types/api';
import type { MonthlyDataPoint } from '../utils/chartHelpers';
import { getSemanticColors } from '../utils/chartTheme';

export interface CategoryDrillCardData {
  categoryId: number;
  name: string;
  planned: number;
  actual: number;
  /** Series ordered oldest -> newest. */
  monthly: MonthlyDataPoint[];
  /** Transactions belonging to this category within the active range. */
  transactions: Transaction[];
}

interface CategoryDrillCardProps {
  data: CategoryDrillCardData;
  expanded: boolean;
  onToggle: () => void;
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

const currencyPrecise = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
});

export function CategoryDrillCard({ data, expanded, onToggle }: CategoryDrillCardProps) {
  const theme = useTheme();
  const semantic = getSemanticColors(theme);

  const pct = data.planned > 0 ? Math.min(100, (data.actual / data.planned) * 100) : 0;
  const over = data.planned > 0 && data.actual > data.planned;
  const barColor = over ? semantic.overspend : semantic.income;

  const sparkData = data.monthly.map((m) => m.expenses);
  const hasSpark = sparkData.some((v) => v > 0);

  return (
    <Card sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      <CardActionArea onClick={onToggle} sx={{ flex: 1 }}>
        <CardContent>
          <Box className="flex items-baseline justify-between gap-2">
            <Typography
              variant="subtitle2"
              sx={{ fontWeight: 700, color: semantic.ink }}
              noWrap
            >
              {data.name}
            </Typography>
            <Typography variant="caption" sx={{ color: over ? semantic.overspend : semantic.expense, fontWeight: 600 }}>
              {data.planned > 0 ? `${Math.round((data.actual / data.planned) * 100)}%` : '—'}
            </Typography>
          </Box>
          <Typography variant="body2" sx={{ color: semantic.expense, mt: 0.5 }}>
            {`${currency.format(data.actual)} / ${currency.format(data.planned)}`}
          </Typography>

          <Box
            sx={{
              mt: 1.25,
              height: 8,
              borderRadius: 4,
              backgroundColor: alpha(semantic.neutral, 0.3),
              overflow: 'hidden',
            }}
          >
            <Box
              sx={{
                width: `${pct}%`,
                height: '100%',
                backgroundColor: barColor,
                borderRadius: 4,
                transition: 'width 200ms ease',
              }}
            />
          </Box>

          <Box sx={{ mt: 1.5, height: 40 }}>
            {hasSpark ? (
              <SparkLineChart
                data={sparkData}
                height={40}
                color={barColor}
                area
                curve="monotoneX"
                showHighlight
              />
            ) : (
              <Typography variant="caption" sx={{ color: semantic.expense }}>
                No 3-month history
              </Typography>
            )}
          </Box>
        </CardContent>
      </CardActionArea>
      <Collapse in={expanded} unmountOnExit>
        <Divider />
        <Box sx={{ px: 2, py: 1 }}>
          {data.transactions.length === 0 ? (
            <Typography variant="caption" sx={{ color: semantic.expense }}>
              No transactions in this range
            </Typography>
          ) : (
            <List dense disablePadding>
              {data.transactions
                .slice()
                .sort((a, b) => b.occurredAt.localeCompare(a.occurredAt))
                .slice(0, 10)
                .map((t) => (
                  <ListItem key={t.id} disableGutters sx={{ py: 0.25 }}>
                    <ListItemText
                      primary={t.payee || data.name}
                      secondary={format(parseISO(t.occurredAt), 'MMM d, yyyy')}
                      primaryTypographyProps={{ variant: 'body2', fontWeight: 500 }}
                      secondaryTypographyProps={{ variant: 'caption' }}
                    />
                    <Typography
                      variant="body2"
                      sx={{
                        fontWeight: 600,
                        color: t.transactionType === 'Income' ? semantic.income : semantic.expense,
                      }}
                    >
                      {t.transactionType === 'Income' ? '+' : '-'}
                      {currencyPrecise.format(Math.abs(t.amount))}
                    </Typography>
                  </ListItem>
                ))}
            </List>
          )}
        </Box>
      </Collapse>
    </Card>
  );
}
