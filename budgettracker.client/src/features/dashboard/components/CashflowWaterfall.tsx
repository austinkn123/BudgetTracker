import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { BarChart } from '@mui/x-charts/BarChart';
import { useTheme } from '@mui/material/styles';
import { useMemo } from 'react';
import type { BudgetPlan, Category, Transaction } from '../../../shared/types/api';
import { buildWaterfall } from '../utils/planMath';
import { getSemanticColors } from '../utils/chartTheme';

interface CashflowWaterfallProps {
  plan: BudgetPlan | undefined;
  transactions: Transaction[];
  categories: Category[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

/**
 * Pseudo-waterfall built on `BarChart`. We split the value column into three
 * stacked series (income, expense, net) so each gets a single color via
 * `series.color`, then push a transparent "base" series to float the bars
 * to their proper running-total position.
 */
export function CashflowWaterfall({ plan, transactions, categories }: CashflowWaterfallProps) {
  const theme = useTheme();
  const semantic = getSemanticColors(theme);

  const items = useMemo(
    () => buildWaterfall(transactions, plan, categories),
    [transactions, plan, categories],
  );

  const chartData = useMemo(() => {
    if (items.length === 0) {
      return null;
    }
    const bases: number[] = [];
    const incomeData: (number | null)[] = [];
    const expenseData: (number | null)[] = [];
    const netPositive: (number | null)[] = [];
    const netNegative: (number | null)[] = [];

    let running = 0;
    for (const item of items) {
      if (item.kind === 'net') {
        bases.push(item.signed >= 0 ? 0 : item.signed);
        incomeData.push(null);
        expenseData.push(null);
        if (item.signed >= 0) {
          netPositive.push(item.value);
          netNegative.push(null);
        } else {
          netPositive.push(null);
          netNegative.push(item.value);
        }
        continue;
      }

      if (item.kind === 'income') {
        bases.push(running);
        incomeData.push(item.value);
        expenseData.push(null);
        netPositive.push(null);
        netNegative.push(null);
        running += item.value;
      } else {
        const newRunning = running - item.value;
        bases.push(newRunning);
        incomeData.push(null);
        expenseData.push(item.value);
        netPositive.push(null);
        netNegative.push(null);
        running = newRunning;
      }
    }

    return { bases, incomeData, expenseData, netPositive, netNegative };
  }, [items]);

  if (!plan || !chartData) {
    return (
      <Card className="h-full">
        <CardContent className="flex flex-col items-center justify-center h-full min-h-[320px]">
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
            Cashflow Waterfall
          </Typography>
          <Typography variant="body2" color="text.secondary">
            No transactions yet this plan month
          </Typography>
        </CardContent>
      </Card>
    );
  }

  const labels = items.map((i) => i.label);

  return (
    <Card className="h-full">
      <CardContent>
        <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
          Cashflow Waterfall
        </Typography>
        <BarChart
          height={320}
          xAxis={[{ data: labels, scaleType: 'band' }]}
          series={[
            {
              data: chartData.bases,
              stack: 'waterfall',
              color: 'transparent',
              label: 'base',
              valueFormatter: () => null,
            },
            {
              data: chartData.incomeData,
              stack: 'waterfall',
              color: semantic.income,
              label: 'Income',
              valueFormatter: (v) => (v == null ? '' : `+${currency.format(v)}`),
            },
            {
              data: chartData.expenseData,
              stack: 'waterfall',
              color: semantic.expense,
              label: 'Expense',
              valueFormatter: (v) => (v == null ? '' : `-${currency.format(v)}`),
            },
            {
              data: chartData.netPositive,
              stack: 'waterfall',
              color: semantic.income,
              label: 'Net surplus',
              valueFormatter: (v) => (v == null ? '' : `+${currency.format(v)}`),
            },
            {
              data: chartData.netNegative,
              stack: 'waterfall',
              color: semantic.overspend,
              label: 'Net deficit',
              valueFormatter: (v) => (v == null ? '' : `-${currency.format(v)}`),
            },
          ]}
          slotProps={{
            legend: {
              direction: 'horizontal',
              position: { vertical: 'top', horizontal: 'center' },
            },
          }}
        />
      </CardContent>
    </Card>
  );
}
