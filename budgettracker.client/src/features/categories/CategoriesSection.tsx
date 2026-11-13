import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Trash2 } from 'lucide-react';
import {
  Badge,
  Button,
  Card,
  ConfirmModal,
  Input,
  Modal,
  ModalActions,
  Select,
  Skeleton,
  ToggleGroup,
  Tooltip,
} from '../../shared/components/ui';
import type { Category } from '../../shared/types/api';
import { PLAID_CATEGORY_OPTIONS } from '../../shared/constants/plaidCategories';
import { categorySchema, type CategoryFormValues } from '../../shared/validation/categorySchema';
import { currency } from '../../shared/utils/format';
import { useBudgetPlans } from '../budget-plans/hooks/useBudgetPlans';
import { useCategories } from './hooks/useCategories';
import { useCategoryMutations } from './hooks/useCategoryMutations';
import { useCategoryUsage } from './hooks/useCategoryUsage';
import {
  rankCategories,
  usageSince,
  type CategorySegment,
  type RankedCategory,
} from './utils/rankCategories';

type CategoriesSectionProps = {
  isLoading: boolean;
  setStatusMessage: (message: string | null) => void;
  setStatusError: (message: string | null) => void;
};

const WINDOW_MONTHS = 12;

const SEGMENTS = [
  { value: 'spending', label: 'Spending' },
  { value: 'income', label: 'Income' },
  { value: 'transfers', label: 'Transfers' },
] as const;

// One measure per segment, so one colour per segment: spending stays the neutral expense slate.
const SEGMENT_STYLE: Record<CategorySegment, { bar: string; measure: string; empty: string }> = {
  spending: { bar: 'bg-ink-muted', measure: 'spend', empty: 'No spending categories yet.' },
  income: { bar: 'bg-success', measure: 'income', empty: 'No income categories yet.' },
  transfers: { bar: 'bg-secondary', measure: 'amount moved', empty: 'No transfer categories yet.' },
};

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`;

const CATEGORY_TYPE_OPTIONS = [
  { value: 'Income', label: 'Income' },
  { value: 'Expense', label: 'Expense' },
  { value: 'Both', label: 'Both' },
  { value: 'Transfer', label: 'Transfer (not spending)' },
] as const;

const CategoriesSection = ({
  isLoading,
  setStatusMessage,
  setStatusError,
}: CategoriesSectionProps) => {
  const { data: categories = [] } = useCategories();
  // A calendar date, so the key holds for the whole day instead of changing every render.
  const since = usageSince(new Date(), WINDOW_MONTHS);
  const { data: usage = [] } = useCategoryUsage(since);
  const { data: budgetPlans = [] } = useBudgetPlans();
  const { createCategory, updateCategory, deleteCategory } = useCategoryMutations({
    setStatusMessage,
    setStatusError,
  });

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'add' | 'edit'>('add');
  const [editingCategoryId, setEditingCategoryId] = useState<number | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<RankedCategory | null>(null);
  const [segment, setSegment] = useState<CategorySegment>('spending');
  const { control, handleSubmit, reset } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: {
      name: '',
      categoryType: 'Expense',
    },
  });

  const rows = useMemo(
    () => rankCategories(categories, usage, budgetPlans, segment),
    [categories, usage, budgetPlans, segment],
  );
  // Nothing filed here and no plan line: deleting it changes no figure, so it waits apart.
  const used = rows.filter((r) => r.transactions > 0 || r.planEntries > 0);
  const unused = rows.filter((r) => r.transactions === 0 && r.planEntries === 0);
  const max = used.reduce((m, r) => Math.max(m, r.total), 0);
  const sum = used.reduce((s, r) => s + Math.max(r.total, 0), 0);
  const style = SEGMENT_STYLE[segment];

  if (isLoading) {
    return (
      <Card title="Categories">
        <div className="space-y-4" aria-busy>
          {[0, 1, 2, 3, 4].map((i) => (
            <div key={i} className="flex items-center gap-4">
              <div className="w-40 space-y-1.5">
                <Skeleton width={96 + ((i * 23) % 48)} height={12} />
                <Skeleton width={80} height={10} />
              </div>
              <Skeleton height={6} className="flex-1 rounded-full" />
              <Skeleton width={64} height={12} />
            </div>
          ))}
        </div>
      </Card>
    );
  }

  const openAddDialog = () => {
    setEditingCategoryId(null);
    reset({ name: '', categoryType: 'Expense', plaidCategoryPrimary: '' });
    setDialogMode('add');
    setDialogOpen(true);
  };

  const openEditDialog = (category: Category) => {
    const categoryType =
      category.categoryType === 'Income' ||
      category.categoryType === 'Both' ||
      category.categoryType === 'Transfer'
        ? category.categoryType
        : 'Expense';

    setEditingCategoryId(category.id);
    reset({
      name: category.name,
      categoryType,
      plaidCategoryPrimary: category.plaidCategoryPrimary ?? '',
    });
    setDialogMode('edit');
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setEditingCategoryId(null);
  };

  const onSave = handleSubmit(async (values) => {
    try {
      if (dialogMode === 'add') {
        await createCategory.mutateAsync({
          userId: 0,
          name: values.name,
          categoryType: values.categoryType,
          plaidCategoryPrimary: values.plaidCategoryPrimary ?? '',
        });
        closeDialog();
        return;
      }

      if (editingCategoryId === null) return;

      await updateCategory.mutateAsync({
        id: editingCategoryId,
        userId: 0,
        name: values.name,
        categoryType: values.categoryType,
        // Always sent — omitting it makes the server treat the mapping as "not supplied",
        // which used to wipe auto-categorisation on a simple rename.
        plaidCategoryPrimary: values.plaidCategoryPrimary ?? '',
      });
      closeDialog();
    } catch {
      // Mutation hooks handle status messaging.
    }
  });

  const onConfirmDelete = async () => {
    if (!deleteTarget) return;
    await deleteCategory.mutateAsync(deleteTarget.category.id);
    setDeleteTarget(null);
  };

  const isSaving = createCategory.isPending || updateCategory.isPending || deleteCategory.isPending;

  return (
    <Card
      title="Categories"
      subtitle={`Ranked by average monthly ${style.measure} over the last ${WINDOW_MONTHS} months. Select one to rename it or change its type.`}
      actions={
        <>
          <ToggleGroup value={segment} onChange={setSegment} options={SEGMENTS} ariaLabel="Category type" />
          <Button size="sm" onClick={openAddDialog}>
            Add category
          </Button>
        </>
      }
    >
      {categories.length === 0 ? (
        <p className="text-sm text-ink-muted">
          No categories yet. Add one for each kind of spending you want to see in your plan, like
          groceries or the mortgage.
        </p>
      ) : rows.length === 0 ? (
        <p className="text-sm text-ink-muted">{style.empty}</p>
      ) : (
        <>
          <ul className="flex flex-col">
            {used.map((row, idx) => {
              const value = Math.max(row.total, 0);
              const notInPlan = segment === 'spending' && value > 0 && !row.inActivePlan;
              return (
                <li
                  key={row.category.id}
                  className="flex items-center gap-1 border-b border-border-subtle last:border-b-0"
                >
                  {/* Native <button>: full-row click target laid out as a 4-column grid; Button forces inline-flex + nowrap. */}
                  <button
                    type="button"
                    onClick={() => openEditDialog(row.category)}
                    className="focus-ring -mx-2 grid min-w-0 flex-1 grid-cols-[minmax(0,13rem)_1fr_5.5rem_2.5rem] items-center gap-4 rounded-md px-2 py-2.5 text-left transition-colors duration-120 hover:bg-background"
                  >
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-medium text-ink">{row.category.name}</span>
                      <span className="block text-xs text-ink-muted">
                        {plural(row.transactions, 'transaction')}
                        {row.inActivePlan && ' · in plan'}
                        {notInPlan && <span className="font-semibold text-warning-dark"> · not in plan</span>}
                      </span>
                    </span>
                    <Tooltip title={`${currency.format(value)} over the last ${WINDOW_MONTHS} months`}>
                      <span className="block h-1.5 overflow-hidden rounded-full bg-border-subtle">
                        <span
                          className={`block h-full origin-left animate-grow-x rounded-full ${style.bar}`}
                          style={{ width: `${max > 0 ? (value / max) * 100 : 0}%`, animationDelay: `${idx * 30}ms` }}
                        />
                      </span>
                    </Tooltip>
                    <span className="text-right text-sm font-semibold numeric text-ink">
                      {currency.format(value / WINDOW_MONTHS)}
                      <span className="text-xs font-normal text-ink-muted">/mo</span>
                    </span>
                    <span className="text-right text-xs numeric text-ink-muted">
                      {sum > 0 ? Math.round((value / sum) * 100) : 0}%
                    </span>
                  </button>
                  <Tooltip title={`Delete ${row.category.name}`}>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => setDeleteTarget(row)}
                      aria-label={`Delete ${row.category.name}`}
                      className="h-8 w-8 shrink-0 rounded-full p-0 hover:bg-error/10 hover:text-error"
                    >
                      <Trash2 size={15} />
                    </Button>
                  </Tooltip>
                </li>
              );
            })}
          </ul>

          {unused.length > 0 && (
            <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-border-subtle pt-3 text-xs text-ink-muted">
              <span className="font-semibold">Never used</span>
              {unused.map((row) => (
                <Badge
                  key={row.category.id}
                  label={row.category.name}
                  variant="outline"
                  onClick={() => openEditDialog(row.category)}
                  onDelete={() => setDeleteTarget(row)}
                />
              ))}
              <span>No transactions or plan lines, so deleting one changes no figure.</span>
            </div>
          )}
        </>
      )}

      <Modal
        open={dialogOpen}
        onClose={closeDialog}
        title={dialogMode === 'add' ? 'Add category' : 'Edit category'}
        maxWidth="xs"
        disableBackdropClose={isSaving}
        actions={
          <ModalActions
            onCancel={closeDialog}
            onConfirm={onSave}
            confirmLabel={dialogMode === 'add' ? 'Create' : 'Save'}
            isPending={isSaving}
          />
        }
      >
        <Input control={control} name="name" label="Name" autoFocus />
        <Select
          control={control}
          name="categoryType"
          label="Type"
          options={CATEGORY_TYPE_OPTIONS}
        />
        <Select
          control={control}
          name="plaidCategoryPrimary"
          label="Auto-categorize imports as"
          options={PLAID_CATEGORY_OPTIONS}
          emptyOptionLabel="No automatic mapping"
          helperText="Imported transactions Plaid tags with this category are assigned here automatically."
        />
      </Modal>

      <ConfirmModal
        open={deleteTarget !== null}
        title="Delete category?"
        message={
          deleteTarget ? (
            <>
              Delete <strong>{deleteTarget.category.name}</strong>? It is used in{' '}
              {deleteTarget.transactions} transactions and {deleteTarget.planEntries} budget plan
              entries. This can't be undone.
            </>
          ) : (
            ''
          )
        }
        confirmLabel="Delete"
        destructive
        isPending={isSaving}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={onConfirmDelete}
      />
    </Card>
  );
};

export default CategoriesSection;
