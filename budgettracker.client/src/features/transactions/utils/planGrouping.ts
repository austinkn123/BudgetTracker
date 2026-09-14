import type {
  BudgetPlan,
  BudgetPlanEntry,
  CategoryPerformance,
  Transaction,
} from '../../../shared/types/api';

export type Bucket = 'Core' | 'Buffer';

export type CategoryGroup = {
  /** Null for the uncategorised group. */
  categoryId: number | null;
  name: string;
  planned: number;
  actual: number;
  /** Positive when actual has exceeded planned. Mirrors the server's CategoryPerformance.OverBy. */
  overBy: number;
  transactions: Transaction[];
};

export type BucketGroup = {
  bucket: Bucket;
  planned: number;
  actual: number;
  categories: CategoryGroup[];
};

export type PlanGrouping = {
  /** Pinned above the buckets — this is the work, and it is invisible to every plan figure. */
  uncategorized: CategoryGroup | null;
  buckets: BucketGroup[];
};

/**
 * Which bucket each category belongs to, from the plan's entries.
 *
 * Two domain rules are load-bearing here:
 *  - Planned figures come from `monthlyEquivalent`, never `amount`. An Annual line of $2,004 is
 *    $167/month; comparing spend against $2,004 would invent a catastrophic overspend.
 *  - Multiple entries may share a category, in which case the planned amounts sum. The server
 *    resolves the bucket last-write-wins in SortOrder; we match that so both agree.
 */
export const buildCategoryPlanIndex = (entries: readonly BudgetPlanEntry[]) => {
  const plannedByCategory = new Map<number, number>();
  const bucketByCategory = new Map<number, Bucket>();

  for (const entry of entries) {
    if (entry.lineType !== 'Expense') continue;
    if (entry.categoryId == null) continue;

    plannedByCategory.set(
      entry.categoryId,
      (plannedByCategory.get(entry.categoryId) ?? 0) + entry.monthlyEquivalent,
    );
    bucketByCategory.set(entry.categoryId, entry.bucket === 'Core' ? 'Core' : 'Buffer');
  }

  return { plannedByCategory, bucketByCategory };
};

/**
 * Group a month's transactions by bucket, then category, against the governing plan.
 *
 * Mirrors `BudgetAnalysisEngine.AnalyzeMonth`: only Expense rows count toward actuals, amounts are
 * accumulated as magnitudes (they arrive signed), and **spend in a category the plan doesn't budget
 * for falls into Buffer** — as does uncategorised spend. That is the budgeting philosophy: you
 * commit to Core, and everything else eats your Buffer.
 *
 * `serverPerformance` is preferred for planned/actual when supplied, so the page and the dashboard
 * never disagree; the transactions themselves are only ever grouped locally.
 */
export const groupByPlan = (
  transactions: readonly Transaction[],
  plan: BudgetPlan | null,
  categoryNames: ReadonlyMap<number, string>,
  serverPerformance?: readonly CategoryPerformance[],
): PlanGrouping => {
  const { plannedByCategory, bucketByCategory } = buildCategoryPlanIndex(plan?.entries ?? []);

  const plannedFor = (categoryId: number) => {
    const fromServer = serverPerformance?.find((p) => p.categoryId === categoryId);
    return fromServer?.planned ?? plannedByCategory.get(categoryId) ?? 0;
  };

  const expenses = transactions.filter((t) => t.transactionType === 'Expense');

  const uncategorizedTransactions = expenses.filter((t) => t.categoryId == null);
  const byCategory = new Map<number, Transaction[]>();
  for (const transaction of expenses) {
    if (transaction.categoryId == null) continue;
    const existing = byCategory.get(transaction.categoryId);
    if (existing) existing.push(transaction);
    else byCategory.set(transaction.categoryId, [transaction]);
  }

  // Categories with a plan line but no spend still belong on the page — an untouched budget is
  // information, not an empty row to hide.
  const categoryIds = new Set<number>([...byCategory.keys(), ...plannedByCategory.keys()]);

  const groups: (CategoryGroup & { bucket: Bucket })[] = [];
  for (const categoryId of categoryIds) {
    const rows = byCategory.get(categoryId) ?? [];
    const actual = rows.reduce((sum, t) => sum + Math.abs(t.amount), 0);
    const planned = plannedFor(categoryId);

    groups.push({
      categoryId,
      name: categoryNames.get(categoryId) ?? `Category ${categoryId}`,
      planned,
      actual,
      overBy: actual - planned,
      transactions: rows,
      bucket: bucketByCategory.get(categoryId) ?? 'Buffer',
    });
  }

  const buckets: BucketGroup[] = (['Core', 'Buffer'] as const).map((bucket) => {
    const categories = groups
      .filter((g) => g.bucket === bucket)
      // Worst overspend first: the row that needs attention should not require scrolling.
      .sort((a, b) => b.overBy - a.overBy)
      .map((group) => ({
        categoryId: group.categoryId,
        name: group.name,
        planned: group.planned,
        actual: group.actual,
        overBy: group.overBy,
        transactions: group.transactions,
      }));

    return {
      bucket,
      planned: categories.reduce((sum, c) => sum + c.planned, 0),
      actual: categories.reduce((sum, c) => sum + c.actual, 0),
      categories,
    };
  });

  const uncategorizedActual = uncategorizedTransactions.reduce(
    (sum, t) => sum + Math.abs(t.amount),
    0,
  );

  return {
    uncategorized:
      uncategorizedTransactions.length > 0
        ? {
            categoryId: null,
            name: 'Uncategorized',
            planned: 0,
            actual: uncategorizedActual,
            overBy: uncategorizedActual,
            transactions: uncategorizedTransactions,
          }
        : null,
    buckets,
  };
};

/**
 * Where a category sits on the 4-step semantic ramp (see ui-modernization.md §4.1).
 * Nothing planned means nothing to be over, so an unbudgeted category reads neutral rather than
 * as a failure — it is Buffer spend, which is allowed.
 */
export type SpendStatus = 'none' | 'on-pace' | 'warn-low' | 'warn-high' | 'over';

export const spendStatus = (planned: number, actual: number): SpendStatus => {
  if (planned <= 0) return 'none';
  const ratio = actual / planned;
  if (ratio <= 1) return 'on-pace';
  if (ratio <= 1.2) return 'warn-low';
  if (ratio <= 1.5) return 'warn-high';
  return 'over';
};
