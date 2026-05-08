import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { LineChart } from '@mui/x-charts/LineChart';
import type { MonthlyDataPoint } from '../../utils/chartHelpers';

interface SpendingOverTimeChartProps {
  data: MonthlyDataPoint[];
}

export function SpendingOverTimeChart({ data }: SpendingOverTimeChartProps) {
  if (data.length === 0) {
    return (
      <Card className="h-full rounded-2xl border border-slate-200/80 bg-white shadow-sm" elevation={0}>
        <CardContent className="flex flex-col items-center justify-center h-full min-h-[300px]">
          <Typography variant="h6" className="font-semibold mb-2 text-slate-900">
            Spending Over Time
          </Typography>
          <Typography variant="body2" className="text-slate-500">
            No transaction data yet
          </Typography>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="h-full rounded-2xl border border-slate-200/80 bg-white shadow-sm" elevation={0}>
      <CardContent className="p-6">
        <Typography variant="h6" className="font-semibold mb-1 text-slate-900">
          Spending Over Time
        </Typography>
        <Typography variant="body2" className="mb-4 text-slate-500">
          Monthly income and expense trends
        </Typography>
        <LineChart
          xAxis={[
            {
              data: data.map((_, i) => i),
              scaleType: 'point',
              valueFormatter: (index: number) => data[index]?.month ?? '',
            },
          ]}
          series={[
            {
              data: data.map((d) => d.income),
              label: 'Income',
              color: '#16a34a',
              curve: 'natural',
              area: true,
            },
            {
              data: data.map((d) => d.expenses),
              label: 'Expenses',
              color: '#dc2626',
              curve: 'natural',
              area: true,
            },
          ]}
          height={300}
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
