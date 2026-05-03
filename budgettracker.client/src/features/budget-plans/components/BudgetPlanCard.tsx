import { format } from 'date-fns';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CardHeader from '@mui/material/CardHeader';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import { alpha } from '@mui/material/styles';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import type { BudgetPlan, BudgetPlanEntry } from '../../../shared/types/api';

type BudgetPlanCardProps = {
  plan: BudgetPlan;
  categoryNameById: Map<number, string>;
  isSwitchingPlan: boolean;
  onAddLine: (planId: number) => void;
  onEditLine: (planId: number, line: BudgetPlanEntry) => void;
  onSwitchActive: (planId: number) => void;
  onEditPlan: (plan: BudgetPlan) => void;
  onDeletePlan: (plan: BudgetPlan) => void;
};

const BudgetPlanCard = ({
  plan,
  categoryNameById,
  isSwitchingPlan,
  onAddLine,
  onEditLine,
  onSwitchActive,
  onEditPlan,
  onDeletePlan,
}: BudgetPlanCardProps) => {
  const [planYear, planMonth] = plan.planMonth.slice(0, 7).split('-').map(Number);
  const planMonthLabel = Number.isFinite(planYear) && Number.isFinite(planMonth)
    ? format(new Date(planYear, planMonth - 1, 1), 'MMMM yyyy')
    : plan.planMonth;

  const monthlyIncome = plan.netIncomeMonthly;

  const monthlyExpenses = plan.entries
    .filter((entry) => entry.lineType === 'Expense')
    .reduce((sum, entry) => sum + entry.monthlyEquivalent, 0);

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
        subheader={planMonthLabel}
        action={
          <Stack direction="row" spacing={1} className="items-center justify-end flex-wrap">
            {!plan.isActive && (
              <Button
                size="small"
                variant="outlined"
                onClick={() => onSwitchActive(plan.id)}
                disabled={isSwitchingPlan}
              >
                Set Active
              </Button>
            )}
            <Button
              size="small"
              startIcon={<Plus className="w-4 h-4" />}
              onClick={() => onAddLine(plan.id)}
            >
              Add Entry
            </Button>
            <Button
              size="small"
              color="inherit"
              startIcon={<Pencil className="w-4 h-4" />}
              onClick={() => onEditPlan(plan)}
            >
              Edit
            </Button>
            <Button
              size="small"
              color="error"
              startIcon={<Trash2 className="w-4 h-4" />}
              onClick={() => onDeletePlan(plan)}
            >
              Delete
            </Button>
          </Stack>
        }
      />
      <CardContent className="pt-0">
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: 'repeat(3, minmax(0, 1fr))' },
            gap: 1.5,
            mb: 3,
          }}
        >
          <Box
            sx={{
              borderRadius: 3,
              px: 1.5,
              py: 1.25,
              border: (theme) => `1px solid ${alpha(theme.palette.success.main, 0.2)}`,
              backgroundColor: (theme) => alpha(theme.palette.success.main, 0.12),
            }}
          >
            <Typography variant="caption" color="success.main">Monthly Income</Typography>
            <Typography variant="subtitle1" fontWeight={600} color="success.dark">
              ${monthlyIncome.toFixed(2)}
            </Typography>
          </Box>
          <Box
            sx={{
              borderRadius: 3,
              px: 1.5,
              py: 1.25,
              border: (theme) => `1px solid ${alpha(theme.palette.error.main, 0.2)}`,
              backgroundColor: (theme) => alpha(theme.palette.error.main, 0.08),
            }}
          >
            <Typography variant="caption" color="error.main">Monthly Expenses</Typography>
            <Typography variant="subtitle1" fontWeight={600} color="error.dark">
              ${monthlyExpenses.toFixed(2)}
            </Typography>
          </Box>
          <Box
            sx={{
              borderRadius: 3,
              px: 1.5,
              py: 1.25,
              border: (theme) => `1px solid ${alpha(theme.palette.primary.main, 0.2)}`,
              backgroundColor: (theme) => alpha(theme.palette.secondary.main, 0.12),
            }}
          >
            <Typography variant="caption" color="primary.main">Monthly Net</Typography>
            <Typography variant="subtitle1" fontWeight={600} color={monthlyNet >= 0 ? 'success.dark' : 'error.dark'}>
              ${monthlyNet.toFixed(2)}
            </Typography>
          </Box>
        </Box>

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
              {plan.entries.length > 0 ? (
                plan.entries
                  .slice()
                  .sort((a, b) => a.sortOrder - b.sortOrder)
                  .map((entry) => (
                    <TableRow
                      key={entry.id}
                      hover
                      onClick={() => onEditLine(plan.id, entry)}
                      className="cursor-pointer"
                    >
                      <TableCell>
                        {entry.categoryId
                          ? (categoryNameById.get(entry.categoryId) ?? `Unknown`)
                          : '-'}
                      </TableCell>
                      <TableCell>
                        <Chip
                          label={entry.lineType}
                          size="small"
                          color={entry.lineType === 'Income' ? 'success' : 'error'}
                          variant="outlined"
                        />
                      </TableCell>
                      <TableCell>{entry.bucket}</TableCell>
                      <TableCell>{entry.cadence}</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 600 }}>
                        ${entry.amount.toFixed(2)}
                      </TableCell>
                      <TableCell align="right">${entry.monthlyEquivalent.toFixed(2)}</TableCell>
                    </TableRow>
                  ))
              ) : (
                <TableRow>
                  <TableCell colSpan={6} align="center">
                    <Typography color="text.secondary" fontStyle="italic">
                      No plan entries — click "Add Entry" to get started
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
