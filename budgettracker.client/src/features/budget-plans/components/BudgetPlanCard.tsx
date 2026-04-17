import { format } from 'date-fns';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CardHeader from '@mui/material/CardHeader';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import { Plus } from 'lucide-react';
import type { BudgetPlan, BudgetPlanLine } from '../../../shared/types/api';

type BudgetPlanCardProps = {
  plan: BudgetPlan;
  categoryNameById: Map<number, string>;
  onAddLine: (planId: number) => void;
  onEditLine: (planId: number, line: BudgetPlanLine) => void;
};

const BudgetPlanCard = ({ plan, categoryNameById, onAddLine, onEditLine }: BudgetPlanCardProps) => {
  const monthlyIncome = plan.lines
    .filter((line) => line.lineType === 'Income')
    .reduce((sum, line) => sum + line.monthlyEquivalent, 0);

  const monthlyExpenses = plan.lines
    .filter((line) => line.lineType === 'Expense')
    .reduce((sum, line) => sum + line.monthlyEquivalent, 0);

  const monthlyNet = monthlyIncome - monthlyExpenses;

  return (
    <Card variant="outlined">
      <CardHeader
        title={
          <div className="flex items-center gap-2">
            <Typography variant="h6">{plan.name}</Typography>
            <Chip
              label={plan.isActive ? 'Active' : 'Inactive'}
              color={plan.isActive ? 'success' : 'default'}
              size="small"
            />
          </div>
        }
        subheader={format(new Date(plan.planMonth), 'MMMM yyyy')}
        action={
          <Button
            size="small"
            startIcon={<Plus className="w-4 h-4" />}
            onClick={() => onAddLine(plan.id)}
          >
            Add Line
          </Button>
        }
      />
      <CardContent className="pt-0">
        <div className="grid grid-cols-3 gap-3 mb-4">
          <div className="rounded-lg bg-green-50 px-3 py-2">
            <Typography variant="caption" color="success.main">Monthly Income</Typography>
            <Typography variant="subtitle1" fontWeight={600} color="success.dark">
              ${monthlyIncome.toFixed(2)}
            </Typography>
          </div>
          <div className="rounded-lg bg-red-50 px-3 py-2">
            <Typography variant="caption" color="error.main">Monthly Expenses</Typography>
            <Typography variant="subtitle1" fontWeight={600} color="error.dark">
              ${monthlyExpenses.toFixed(2)}
            </Typography>
          </div>
          <div className="rounded-lg bg-blue-50 px-3 py-2">
            <Typography variant="caption" color="primary.main">Monthly Net</Typography>
            <Typography variant="subtitle1" fontWeight={600} color={monthlyNet >= 0 ? 'success.dark' : 'error.dark'}>
              ${monthlyNet.toFixed(2)}
            </Typography>
          </div>
        </div>

        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Category</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Bucket</TableCell>
                <TableCell>Cadence</TableCell>
                <TableCell align="right">Amount</TableCell>
                <TableCell align="right">Monthly Eq.</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {plan.lines.length > 0 ? (
                plan.lines
                  .slice()
                  .sort((a, b) => a.sortOrder - b.sortOrder)
                  .map((line) => (
                    <TableRow
                      key={line.id}
                      hover
                      onClick={() => onEditLine(plan.id, line)}
                      className="cursor-pointer"
                    >
                      <TableCell>
                        {line.categoryId
                          ? (categoryNameById.get(line.categoryId) ?? `Unknown`)
                          : '-'}
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={line.lineType}
                          size="small"
                          color={line.lineType === 'Income' ? 'success' : 'error'}
                          variant="outlined"
                        />
                      </TableCell>
                      <TableCell>{line.bucket}</TableCell>
                      <TableCell>{line.cadence}</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 600 }}>
                        ${line.amount.toFixed(2)}
                      </TableCell>
                      <TableCell align="right">${line.monthlyEquivalent.toFixed(2)}</TableCell>
                    </TableRow>
                  ))
              ) : (
                <TableRow>
                  <TableCell colSpan={6} align="center">
                    <Typography color="text.secondary" fontStyle="italic">
                      No plan lines — click "Add Line" to get started
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </CardContent>
    </Card>
  );
};

export default BudgetPlanCard;
