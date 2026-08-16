import { useMemo } from 'react';
import { format } from 'date-fns';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { Badge, Button, Card, Table } from '../../../shared/components/ui';
import type { TableColumn } from '../../../shared/components/ui';
import { cn } from '../../../shared/utils/cn';
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

const TILE_CLASSES = {
  success: 'border-success/20 bg-success/10 [&>span]:text-success [&>p]:text-success-dark',
  error: 'border-error/20 bg-error/10 [&>span]:text-error [&>p]:text-error-dark',
  primary: 'border-primary/20 bg-primary/10 [&>span]:text-primary',
} as const;

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
  tone: keyof typeof TILE_CLASSES;
  signed?: boolean;
}) => (
  <div className={cn('rounded-md border px-3 py-2.5', TILE_CLASSES[tone])}>
    <span className="text-xs">{label}</span>
    <p
      className={cn(
        'text-body font-semibold',
        signed && (value >= 0 ? 'text-success-dark' : 'text-error-dark'),
        !signed && tone === 'primary' && 'text-primary-dark',
      )}
    >
      ${value.toFixed(2)}
    </p>
  </div>
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

  const columns: TableColumn<BudgetPlanEntry>[] = useMemo(
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
          <Badge
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
          <span className="text-sm font-semibold text-ink">${entry.amount.toFixed(2)}</span>
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
    <Card
      title={
        <span className="flex items-center gap-2">
          {plan.name}
          <Badge
            label={plan.isActive ? 'Active' : 'Inactive'}
            color={plan.isActive ? 'success' : 'neutral'}
          />
        </span>
      }
      subtitle={planMonthLabel}
      actions={
        <div className="flex flex-row flex-wrap justify-end gap-2">
          {!plan.isActive && (
            <Button
              size="sm"
              variant="secondary"
              onClick={() => onSwitchActive(plan.id)}
              disabled={isSwitchingPlan}
            >
              Set Active
            </Button>
          )}
          <Button size="sm" variant="ghost" startIcon={<Plus size={16} />} onClick={() => onAddLine(plan.id)}>
            Add Entry
          </Button>
          <Button size="sm" variant="ghost" startIcon={<Pencil size={16} />} onClick={() => onEditPlan(plan)}>
            Edit
          </Button>
          <Button
            size="sm"
            variant="destructive-ghost"
            startIcon={<Trash2 size={16} />}
            onClick={() => onDeletePlan(plan)}
          >
            Delete
          </Button>
        </div>
      }
    >
      <div className="mb-6 grid grid-cols-1 gap-3 md:grid-cols-3">
        <SummaryTile label="Monthly Income" value={monthlyIncome} tone="success" />
        <SummaryTile label="Monthly Expenses" value={monthlyExpenses} tone="error" />
        <SummaryTile label="Monthly Net" value={monthlyNet} tone="primary" signed />
      </div>

      <Table
        columns={columns}
        rows={sortedEntries}
        rowKey={(entry) => entry.id}
        onRowClick={(entry) => onEditLine(plan.id, entry)}
        emptyMessage='No plan entries — click "Add Entry" to get started'
        ariaLabel={`${plan.name} plan entries`}
      />
    </Card>
  );
};

export default BudgetPlanCard;
