import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import { CalendarDays } from 'lucide-react';
import { format, parseISO } from 'date-fns';
import type { BudgetPlan } from '../../../shared/types/api';

interface ActiveBudgetPlanSummaryProps {
  plan: BudgetPlan | undefined;
}

const formatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

export function ActiveBudgetPlanSummary({ plan }: ActiveBudgetPlanSummaryProps) {
  if (!plan) {
    return (
      <Card className="h-full">
        <CardContent className="flex flex-col items-center justify-center h-full min-h-[200px]">
          <Typography variant="h6" className="font-semibold mb-2">
            Active Budget Plan
          </Typography>
          <Typography variant="body2" color="text.secondary">
            No active budget plan
          </Typography>
        </CardContent>
      </Card>
    );
  }

  const totalIncome = plan.netIncomeMonthly;

  const totalExpenses = plan.entries
    .filter((l) => l.lineType === 'Expense')
    .reduce((sum, l) => sum + l.monthlyEquivalent, 0);

  const plannedNet = totalIncome - totalExpenses;

  return (
    <Card className="h-full">
      <CardContent>
        <div className="flex items-center justify-between mb-4">
          <Typography variant="h6" className="font-semibold">
            Active Budget Plan
          </Typography>
          <Chip label="Active" color="success" size="small" />
        </div>

        <Typography variant="subtitle1" className="font-medium">
          {plan.name}
        </Typography>
        <div className="flex items-center gap-1 text-gray-500 mb-4">
          <CalendarDays className="w-4 h-4" />
          <Typography variant="caption">
            {format(parseISO(plan.planMonth), 'MMMM yyyy')}
          </Typography>
        </div>

        <div className="space-y-3">
          <div className="flex justify-between">
            <Typography variant="body2" color="text.secondary">
              Planned Income
            </Typography>
            <Typography variant="body2" className="font-semibold text-green-600">
              {formatter.format(totalIncome)}
            </Typography>
          </div>
          <div className="flex justify-between">
            <Typography variant="body2" color="text.secondary">
              Planned Expenses
            </Typography>
            <Typography variant="body2" className="font-semibold text-red-600">
              {formatter.format(totalExpenses)}
            </Typography>
          </div>
          <div className="border-t border-gray-200 pt-3 flex justify-between">
            <Typography variant="body2" className="font-medium">
              Planned Net
            </Typography>
            <Typography
              variant="body2"
              className={`font-bold ${plannedNet >= 0 ? 'text-blue-600' : 'text-red-600'}`}
            >
              {formatter.format(plannedNet)}
            </Typography>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
