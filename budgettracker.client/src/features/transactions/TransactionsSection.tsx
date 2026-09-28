import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { endOfMonth, isSameMonth, startOfDay, startOfMonth } from 'date-fns';
import { Button, Card, InlineSelect, ToggleGroup } from '../../shared/components/ui';
import { useTransactions } from './hooks/useTransactions';
import { useTransactionReview } from './hooks/useTransactionReview';
import { useCategories } from '../categories/hooks/useCategories';
import { useBudgetPlans } from '../budget-plans/hooks/useBudgetPlans';
import { useBudgetAnalysis } from '../dashboard/hooks/useBudgetAnalysis';
import { plaidService } from '../../shared/services/plaid.service';
import type { TransactionQuery } from '../../shared/services/transaction.service';
import MonthHeader from './components/MonthHeader';
import PlanGroupedView from './components/PlanGroupedView';
import TransactionRows from './components/TransactionRows';
import TransactionTable from './components/TransactionTable';
import TransactionFilters from './components/TransactionFilters';
import { buildListQuery, type TransactionStatusFilter } from './utils/transactionFilters';
import { groupByPlan } from './utils/planGrouping';
import {
  buildTransactionDaySummaries,
  getTransactionDaySummary,
} from './utils/transactionGroups';

type TransactionsSectionProps = {
  isLoading: boolean;
  setStatusMessage: (msg: string | null) => void;
  setStatusError: (msg: string | null) => void;
};

type ViewMode = 'plan' | 'list' | 'calendar';

const VIEW_OPTIONS = [
  { value: 'plan' as const, label: 'Plan' },
  { value: 'list' as const, label: 'List' },
  { value: 'calendar' as const, label: 'Calendar' },
];

const TransactionsSection = ({
  isLoading,
  setStatusMessage,
  setStatusError,
}: TransactionsSectionProps) => {
  const [view, setView] = useState<ViewMode>('plan');
  const [status, setStatus] = useState<TransactionStatusFilter>('all');
  const [search, setSearch] = useState('');
  const [month, setMonth] = useState(() => startOfMonth(new Date()));
  const [selectedDate, setSelectedDate] = useState(() => startOfDay(new Date()));

  const monthStart = useMemo(() => startOfMonth(month), [month]);
  const monthEnd = useMemo(() => endOfMonth(month), [month]);

  // The calendar tints every day of its visible month, so it needs the unfiltered ledger.
  const { data: allTransactions = [] } = useTransactions();

  // Two month queries, deliberately.
  //
  // The List view is a search tool, so its results carry the status chip and the search box.
  // The Plan view is a reckoning: its buckets have to add up to the same month the header reports
  // from the server analysis, which knows nothing about the chips. Feeding it the filtered slice
  // produced contradictory totals with no visible cause, since the chips are not even rendered in
  // Plan view.
  const listQuery = useMemo<TransactionQuery>(
    () => buildListQuery(status, search, monthStart, monthEnd),
    [status, search, monthStart, monthEnd],
  );

  const planQuery = useMemo<TransactionQuery>(
    () => buildListQuery('all', '', monthStart, monthEnd),
    [monthStart, monthEnd],
  );

  const { data: listTransactions = [] } = useTransactions(listQuery);
  const { data: planTransactions = [] } = useTransactions(planQuery);
  const { data: categories = [] } = useCategories();
  const { data: budgetPlans = [] } = useBudgetPlans();

  // Analysis now follows the requested window, so this is the plan-vs-actual for the month on screen.
  const { data: analysis } = useBudgetAnalysis(monthStart, monthEnd, 3);

  // Read-only share of the Settings page connection cache (same query key) so we can resolve a
  // Plaid account mask for each imported row. Never mutates.
  const { data: connection } = useQuery({
    queryKey: ['plaid', 'connection'],
    queryFn: plaidService.getConnection,
  });

  const maskByPlaidAccountId = useMemo(
    () => new Map(connection?.accounts.map((a) => [a.plaidAccountId, a.mask]) ?? []),
    [connection],
  );

  // Selection is scoped to the rows actually on screen, so the hook is told which set that is.
  // Calendar has no checkboxes, so it selects nothing.
  const visibleTransactions = useMemo(() => {
    if (view === 'plan') return planTransactions;
    if (view === 'list') return listTransactions;
    return [];
  }, [view, planTransactions, listTransactions]);

  const review = useTransactionReview(visibleTransactions, setStatusMessage, setStatusError);

  const categoryNames = useMemo(
    () => new Map(categories.map((c) => [c.id, c.name])),
    [categories],
  );

  const categoryOptions = useMemo(
    () => categories.map((c) => ({ value: c.id, label: c.name })),
    [categories],
  );

  // The plan governing the month on screen: plans roll forward, so it is the newest active plan
  // dated on or before it — the same rule BudgetAnalysisManager applies server-side.
  const governingPlan = useMemo(() => {
    const candidates = budgetPlans
      .filter((p) => p.isActive && new Date(p.planMonth) <= monthStart)
      .sort((a, b) => new Date(b.planMonth).getTime() - new Date(a.planMonth).getTime());
    return candidates[0] ?? null;
  }, [budgetPlans, monthStart]);

  const grouping = useMemo(
    () =>
      groupByPlan(
        planTransactions,
        governingPlan,
        categoryNames,
        analysis?.planMonth?.byCategory,
      ),
    [planTransactions, governingPlan, categoryNames, analysis],
  );

  // Counted across the whole ledger rather than the month on screen, so the number does not shift
  // as you browse — it is the size of the backlog, not of this page.
  const uncategorizedCount = useMemo(
    () => allTransactions.filter((t) => t.categoryId == null).length,
    [allTransactions],
  );

  const daySummaries = useMemo(
    () => buildTransactionDaySummaries(allTransactions),
    [allTransactions],
  );
  const selectedDaySummary = useMemo(
    () => getTransactionDaySummary(daySummaries, selectedDate),
    [daySummaries, selectedDate],
  );

  const handleMonthChange = (next: Date) => {
    setMonth(next);
    // Keep the calendar's selection inside the month being reviewed.
    setSelectedDate(isSameMonth(next, new Date()) ? startOfDay(new Date()) : next);
  };

  const rowProps = {
    categories,
    maskByPlaidAccountId,
    selectedIds: review.selectedIds,
    onToggleSelected: review.toggleSelected,
    onCategoryChange: review.setCategoryFor,
    onNotesChange: review.setNotesFor,
    isBusy: review.isBusy,
  };

  if (isLoading) return null;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <ToggleGroup
          value={view}
          onChange={setView}
          options={VIEW_OPTIONS}
          ariaLabel="Transactions view"
        />
        {uncategorizedCount > 0 && (
          <Button
            variant={status === 'uncategorized' ? 'primary' : 'secondary'}
            size="sm"
            onClick={() => {
              setStatus(status === 'uncategorized' ? 'all' : 'uncategorized');
              setView('list');
            }}
          >
            {uncategorizedCount} need a category
          </Button>
        )}
      </div>

      <MonthHeader
        month={monthStart}
        onMonthChange={handleMonthChange}
        performance={analysis?.planMonth ?? null}
      />

      {review.selectedIds.size > 0 && (
        <Card padding="sm">
          <div className="flex flex-col gap-2.5 sm:flex-row sm:items-center sm:justify-between">
            <span className="text-sm font-medium text-ink">
              {review.selectedIds.size} selected
            </span>
            <div className="flex items-center gap-2.5">
              <div className="w-56">
                <InlineSelect
                  value={null}
                  onChange={(categoryId) => review.setCategoryForSelected(categoryId)}
                  options={categoryOptions}
                  emptyOptionLabel="Uncategorized"
                  placeholder="Set category…"
                  ariaLabel="Set category for selected transactions"
                  valueAs="number"
                  disabled={review.isBusy}
                />
              </div>
              <Button variant="ghost" size="sm" onClick={review.clearSelection}>
                Clear
              </Button>
            </div>
          </div>
        </Card>
      )}

      {view === 'plan' && <PlanGroupedView grouping={grouping} {...rowProps} />}

      {view === 'list' && (
        <div className="flex flex-col gap-4">
          <TransactionFilters
            status={status}
            onStatusChange={setStatus}
            search={search}
            onSearchChange={setSearch}
            uncategorizedCount={uncategorizedCount}
          />
          <Card padding="none">
            <div className="flex items-center justify-between border-b border-border px-4 py-2.5">
              <span className="text-xs font-medium uppercase tracking-wide text-ink-muted">
                {listTransactions.length}{' '}
                {listTransactions.length === 1 ? 'transaction' : 'transactions'}
              </span>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => review.toggleAll(review.selectedIds.size !== listTransactions.length)}
              >
                {review.selectedIds.size === listTransactions.length && listTransactions.length > 0
                  ? 'Deselect all'
                  : 'Select all'}
              </Button>
            </div>
            <TransactionRows transactions={listTransactions} {...rowProps} />
          </Card>
        </div>
      )}

      {view === 'calendar' && (
        <Card padding="none">
          <TransactionTable
            categories={categories}
            daySummaries={daySummaries}
            selectedDate={selectedDate}
            selectedDaySummary={selectedDaySummary}
            maskByPlaidAccountId={maskByPlaidAccountId}
            onDateChange={setSelectedDate}
            onMonthChange={(next) => handleMonthChange(startOfMonth(next))}
            onRowClick={() => undefined}
          />
        </Card>
      )}
    </div>
  );
};

export default TransactionsSection;
