import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { BarChart } from '@mui/x-charts/BarChart';
import type { BudgetVsActualItem } from '../../utils/chartHelpers';

interface BudgetVsActualChartProps {
  data: BudgetVsActualItem[];
  planName?: string;
}

export function BudgetVsActualChart({ data, planName }: BudgetVsActualChartProps) {
  if (data.length === 0) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center justify-center min-h-[300px]">
          <Typography variant="h6" className="font-semibold mb-2">
            Budget vs Actual
          </Typography>
          <Typography variant="body2" color="text.secondary">
            No active budget plan to compare
          </Typography>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent>
        <div className="flex items-baseline gap-2 mb-4">
          <Typography variant="h6" className="font-semibold">
            Budget vs Actual
          </Typography>
          {planName && (
            <Typography variant="body2" color="text.secondary">
              — {planName}
            </Typography>
          )}
        </div>
        <BarChart
          xAxis={[
            {
              data: data.map((d) => d.category),
              scaleType: 'band',
            },
          ]}
          series={[
            {
              data: data.map((d) => d.budgeted),
              label: 'Budgeted',
              color: '#2563eb',
            },
            {
              data: data.map((d) => d.actual),
              label: 'Actual',
              color: '#f97316',
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
