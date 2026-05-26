import { useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import type { BudgetPlan, Category, Transaction } from '../../../shared/types/api';
import { aggregateByCategoryMonthly, filterTransactionsByRange } from '../utils/chartHelpers';
import { CategoryDrillCard, type CategoryDrillCardData } from './CategoryDrillCard';

interface CategoryDrillGridProps {
  plan: BudgetPlan | undefined;
  transactions: Transaction[];
  categories: Category[];
  start: Date;
  end: Date;
}

const MAX_CARDS = 8;

export function CategoryDrillGrid({
  plan,
  transactions,
  categories,
  start,
  end,
}: CategoryDrillGridProps) {
  const [expandedId, setExpandedId] = useState<number | null>(null);

  const cards: CategoryDrillCardData[] = useMemo(() => {
    if (!plan) return [];

    // Map planned amounts by category for plan-month expense lines.
    const plannedByCategory = new Map<number, number>();
    for (const line of plan.entries) {
      if (line.lineType !== 'Expense' || line.categoryId == null) continue;
      plannedByCategory.set(
        line.categoryId,
        (plannedByCategory.get(line.categoryId) ?? 0) + line.monthlyEquivalent,
      );
    }

    // Plan-month transactions for "actual" computation.
    const monthKey = plan.planMonth.substring(0, 7);
    const monthTxns = transactions.filter(
      (t) => t.occurredAt.substring(0, 7) === monthKey && t.transactionType === 'Expense',
    );
    // BUD-18: Expense amounts are stored signed (negative); aggregate magnitude.
    const actualByCategory = new Map<number, number>();
    for (const t of monthTxns) {
      const magnitude = t.amount < 0 ? -t.amount : t.amount;
      actualByCategory.set(t.categoryId, (actualByCategory.get(t.categoryId) ?? 0) + magnitude);
    }

    // Per-category sparkline series (last 3 months).
    const monthlySeries = aggregateByCategoryMonthly(transactions, categories, 3);

    // Range-scoped transactions for the drill-down panel.
    const inRange = filterTransactionsByRange(transactions, start, end);
    const inRangeByCategory = new Map<number, Transaction[]>();
    for (const t of inRange) {
      const list = inRangeByCategory.get(t.categoryId) ?? [];
      list.push(t);
      inRangeByCategory.set(t.categoryId, list);
    }

    const categoryNames = new Map(categories.map((c) => [c.id, c.name]));
    const allIds = new Set<number>([
      ...plannedByCategory.keys(),
      ...actualByCategory.keys(),
    ]);

    return Array.from(allIds)
      .map<CategoryDrillCardData>((id) => ({
        categoryId: id,
        name: categoryNames.get(id) ?? `Category ${id}`,
        planned: plannedByCategory.get(id) ?? 0,
        actual: actualByCategory.get(id) ?? 0,
        monthly: monthlySeries.get(id) ?? [],
        transactions: inRangeByCategory.get(id) ?? [],
      }))
      .sort((a, b) => b.planned - a.planned || b.actual - a.actual)
      .slice(0, MAX_CARDS);
  }, [plan, transactions, categories, start, end]);

  if (!plan) {
    return null;
  }

  if (cards.length === 0) {
    return (
      <Card>
        <CardContent>
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
            Category Drill-Down
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Add expense line items to your plan to drill into category performance.
          </Typography>
        </CardContent>
      </Card>
    );
  }

  return (
    <Box>
      <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
        Category Drill-Down
      </Typography>
      <Box className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {cards.map((c) => (
          <CategoryDrillCard
            key={c.categoryId}
            data={c}
            expanded={expandedId === c.categoryId}
            onToggle={() =>
              setExpandedId((prev) => (prev === c.categoryId ? null : c.categoryId))
            }
          />
        ))}
      </Box>
    </Box>
  );
}
