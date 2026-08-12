import { useMemo } from 'react';
import { format } from 'date-fns';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import { alpha } from '@mui/material/styles';
import Typography from '@mui/material/Typography';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { BudBadge, BudButton, BudCard, BudTable } from '../../../shared/components/ui';
import type { BudTableColumn } from '../../../shared/components/ui';
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

/** Summary tile for the income/expenses/net row. */
const SummaryTile = ({
  label,
  value,
  tone,
  /** Net swings with its sign; income and expenses keep a fixed tone. */
  signed = false,
}: {
  label: string;
  value: number;
  tone: 'success' | 'error' | 'primary';
  signed?: boolean;
}) => (
  <Box
    sx={{
      borderRadius: 3,
      px: 1.5,
      py: 1.25,
      border: (theme) => `1px solid ${alpha(theme.palette[tone].main, 0.2)}`,
      backgroundColor: (theme) => alpha(theme.palette[tone].main, 0.1),
    }}
  >
    <Typography variant="caption" color={`${tone}.main`}>
      {label}
    </Typography>
    <Typography
      variant="subtitle1"
      fontWeight={600}
      color={signed ? (value >= 0 ? 'success.dark' : 'error.dark') : `${tone}.dark`}
    >
      ${value.toFixed(2)}
    </Typography>
  </Box>
);

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
  const planMonthLabel =
    Number.isFinite(planYear) && Number.isFinite(planMonth)
      ? format(new Date(planYear, planMonth - 1, 1), 'MMMM yyyy')
      : plan.planMonth;

  const monthlyIncome = plan.netIncomeMonthly;

  const monthlyExpenses = plan.entries
    .filter((entry) => entry.lineType === 'Expense')
    .reduce((sum, entry) => sum + entry.monthlyEquivalent, 0);

  const monthlyNet = monthlyIncome - monthlyExpenses;

  const sortedEntries = useMemo(
    () => plan.entries.slice().sort((a, b) => a.sortOrder - b.sortOrder),
    [plan.entries],
  );

  const columns: BudTableColumn<BudgetPlanEntry>[] = useMemo(
    () => [
      {
        key: 'categoryId',
        header: 'Category',
        render: (entry) =>
          entry.categoryId ? (categoryNameById.get(entry.categoryId) ?? 'Unknown') : '-',
      },
      {
        key: 'lineType',
        header: 'Type',
        render: (entry) => (
          <BudBadge
            label={entry.lineType}
            color={entry.lineType === 'Income' ? 'success' : 'error'}
            variant="outline"
          />
        ),
      },
      { key: 'bucket', header: 'Bucket' },
      { key: 'cadence', header: 'Cadence' },
      {
        key: 'amount',
        header: 'Amount',
        align: 'right',
        render: (entry) => (
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            ${entry.amount.toFixed(2)}
          </Typography>
        ),
      },
      {
        key: 'monthlyEquivalent',
        header: 'Monthly Eq.',
        align: 'right',
        render: (entry) => `$${entry.monthlyEquivalent.toFixed(2)}`,
      },
    ],
    [categoryNameById],
  );

  return (
    <BudCard
      title={
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          {plan.name}
          <BudBadge
            label={plan.isActive ? 'Active' : 'Inactive'}
            color={plan.isActive ? 'success' : 'neutral'}
          />
        </Box>
      }
      subtitle={planMonthLabel}
      actions={
        <Stack direction="row" spacing={1} flexWrap="wrap" justifyContent="flex-end">
          {!plan.isActive && (
            <BudButton
              size="sm"
              variant="secondary"
              onClick={() => onSwitchActive(plan.id)}
              disabled={isSwitchingPlan}
            >
              Set Active
            </BudButton>
          )}
          <BudButton size="sm" variant="ghost" startIcon={<Plus size={16} />} onClick={() => onAddLine(plan.id)}>
            Add Entry
          </BudButton>
          <BudButton size="sm" variant="ghost" startIcon={<Pencil size={16} />} onClick={() => onEditPlan(plan)}>
            Edit
          </BudButton>
          <BudButton
            size="sm"
            variant="destructive-ghost"
            startIcon={<Trash2 size={16} />}
            onClick={() => onDeletePlan(plan)}
          >
            Delete
          </BudButton>
        </Stack>
      }
    >
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: 'repeat(3, minmax(0, 1fr))' },
          gap: 1.5,
          mb: 3,
        }}
      >
        <SummaryTile label="Monthly Income" value={monthlyIncome} tone="success" />
        <SummaryTile label="Monthly Expenses" value={monthlyExpenses} tone="error" />
        <SummaryTile label="Monthly Net" value={monthlyNet} tone="primary" signed />
      </Box>

      <BudTable
        columns={columns}
        rows={sortedEntries}
        rowKey={(entry) => entry.id}
        onRowClick={(entry) => onEditLine(plan.id, entry)}
        emptyMessage='No plan entries — click "Add Entry" to get started'
        ariaLabel={`${plan.name} plan entries`}
      />
    </BudCard>
  );
};

export default BudgetPlanCard;
