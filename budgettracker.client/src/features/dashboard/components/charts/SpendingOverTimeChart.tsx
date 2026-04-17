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
      <Card className="h-full">
        <CardContent className="flex flex-col items-center justify-center h-full min-h-[300px]">
          <Typography variant="h6" className="font-semibold mb-2">
            Spending Over Time
          </Typography>
          <Typography variant="body2" color="text.secondary">
            No transaction data yet
          </Typography>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="h-full">
      <CardContent>
        <Typography variant="h6" className="font-semibold mb-4">
          Spending Over Time
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
