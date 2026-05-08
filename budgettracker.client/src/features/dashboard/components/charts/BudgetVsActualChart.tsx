import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { BarChart } from '@mui/x-charts/BarChart';
import type { BudgetVsActualItem } from '../../utils/chartHelpers';

interface BudgetVsActualChartProps {
  data: BudgetVsActualItem[];
  planName?: string;
  hasActivePlan: boolean;
}

export function BudgetVsActualChart({ data, planName, hasActivePlan }: BudgetVsActualChartProps) {
  if (data.length === 0) {
    return (
      <Card className="rounded-2xl border border-slate-200/80 bg-white shadow-sm" elevation={0}>
        <CardContent className="flex flex-col items-center justify-center min-h-[300px]">
          <Typography variant="h6" className="font-semibold mb-2 text-slate-900">
            Budget vs Actual
          </Typography>
          <Typography variant="body2" className="text-slate-500">
            {hasActivePlan
              ? 'No budget line items or matching expense transactions for this plan month yet'
              : 'No active budget plan to compare'}
          </Typography>
          {hasActivePlan && planName && (
            <Typography variant="caption" className="mt-1 text-slate-400">
              Active plan: {planName}
            </Typography>
          )}
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="rounded-2xl border border-slate-200/80 bg-white shadow-sm" elevation={0}>
      <CardContent className="p-6">
        <div className="flex items-baseline gap-2 mb-4">
          <Typography variant="h6" className="font-semibold text-slate-900">
            Budget vs Actual
          </Typography>
          {planName && (
            <Typography variant="body2" className="text-slate-500">
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
