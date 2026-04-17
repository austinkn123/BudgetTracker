import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { PieChart } from '@mui/x-charts/PieChart';
import type { CategorySlice } from '../../utils/chartHelpers';

interface SpendingByCategoryChartProps {
  data: CategorySlice[];
}

const COLORS = ['#2563eb', '#7c3aed', '#0891b2', '#16a34a', '#eab308', '#dc2626', '#ea580c', '#db2777'];

const formatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

export function SpendingByCategoryChart({ data }: SpendingByCategoryChartProps) {
  if (data.length === 0) {
    return (
      <Card className="h-full">
        <CardContent className="flex flex-col items-center justify-center h-full min-h-[300px]">
          <Typography variant="h6" className="font-semibold mb-2">
            Spending by Category
          </Typography>
          <Typography variant="body2" color="text.secondary">
            No expense data yet
          </Typography>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="h-full">
      <CardContent>
        <Typography variant="h6" className="font-semibold mb-4">
          Spending by Category
        </Typography>
        <PieChart
          series={[
            {
              data: data.map((d, i) => ({
                id: d.id,
                value: d.value,
                label: d.label,
                color: COLORS[i % COLORS.length],
              })),
              arcLabel: (item) => formatter.format(item.value),
              arcLabelMinAngle: 30,
              innerRadius: 40,
              paddingAngle: 2,
              cornerRadius: 4,
            },
          ]}
          height={300}
          slotProps={{
            legend: {
              direction: 'vertical',
              position: { vertical: 'middle', horizontal: 'end' },
            },
          }}
        />
      </CardContent>
    </Card>
  );
}
