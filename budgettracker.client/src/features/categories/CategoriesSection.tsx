import { useMemo } from 'react';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  BudBadge,
  BudButton,
  BudCard,
  BudConfirmModal,
  BudInput,
  BudModal,
  BudModalActions,
  BudSelect,
} from '../../shared/components/ui';
import type { Category } from '../../shared/types/api';
import { categorySchema, type CategoryFormValues } from '../../shared/validation/categorySchema';
import { useTransactions } from '../transactions/hooks/useTransactions';
import { useBudgetPlans } from '../budget-plans/hooks/useBudgetPlans';
import { useCategories } from './hooks/useCategories';
import { useCategoryMutations } from './hooks/useCategoryMutations';

type CategoriesSectionProps = {
  isLoading: boolean;
  setStatusMessage: (message: string | null) => void;
  setStatusError: (message: string | null) => void;
};

const GROUPS: { label: string; type: string; color: 'success' | 'error' | 'info' }[] = [
  { label: 'Income', type: 'Income', color: 'success' },
  { label: 'Expense', type: 'Expense', color: 'error' },
  { label: 'Both', type: 'Both', color: 'info' },
];

const CATEGORY_TYPE_OPTIONS = [
  { value: 'Income', label: 'Income' },
  { value: 'Expense', label: 'Expense' },
  { value: 'Both', label: 'Both' },
] as const;

const CategoriesSection = ({
  isLoading,
  setStatusMessage,
  setStatusError,
}: CategoriesSectionProps) => {
  const { data: categories = [] } = useCategories();
  const { data: transactions = [] } = useTransactions();
  const { data: budgetPlans = [] } = useBudgetPlans();
  const { createCategory, updateCategory, deleteCategory } = useCategoryMutations({
    setStatusMessage,
    setStatusError,
  });

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState<'add' | 'edit'>('add');
  const [editingCategoryId, setEditingCategoryId] = useState<number | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Category | null>(null);
  const { control, handleSubmit, reset } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: {
      name: '',
      categoryType: 'Expense',
    },
  });

  const grouped = useMemo(() => {
    const map = new Map<string, typeof categories>();
    for (const cat of categories) {
      const list = map.get(cat.categoryType) ?? [];
      list.push(cat);
      map.set(cat.categoryType, list);
    }
    return map;
  }, [categories]);

  const usageByCategoryId = useMemo(() => {
    const map = new Map<number, { transactions: number; planEntries: number; total: number }>();

    for (const category of categories) {
      map.set(category.id, { transactions: 0, planEntries: 0, total: 0 });
    }

    for (const transaction of transactions) {
      const usage = map.get(transaction.categoryId) ?? { transactions: 0, planEntries: 0, total: 0 };
      usage.transactions += 1;
      usage.total += 1;
      map.set(transaction.categoryId, usage);
    }

    for (const plan of budgetPlans) {
      for (const entry of plan.entries) {
        if (entry.categoryId == null) continue;
        const usage = map.get(entry.categoryId) ?? { transactions: 0, planEntries: 0, total: 0 };
        usage.planEntries += 1;
        usage.total += 1;
        map.set(entry.categoryId, usage);
      }
    }

    return map;
  }, [categories, transactions, budgetPlans]);

  if (isLoading) return null;

  const openAddDialog = () => {
    setEditingCategoryId(null);
    reset({ name: '', categoryType: 'Expense' });
    setDialogMode('add');
    setDialogOpen(true);
  };

  const openEditDialog = (category: Category) => {
    const categoryType =
      category.categoryType === 'Income' || category.categoryType === 'Both'
        ? category.categoryType
        : 'Expense';

    setEditingCategoryId(category.id);
    reset({
      name: category.name,
      categoryType,
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
      });
      closeDialog();
    } catch {
      // Mutation hooks handle status messaging.
    }
  });

  const onConfirmDelete = async () => {
    if (!deleteTarget) return;
    await deleteCategory.mutateAsync(deleteTarget.id);
    setDeleteTarget(null);
  };

  const getUsage = (categoryId: number) => {
    return usageByCategoryId.get(categoryId) ?? { transactions: 0, planEntries: 0, total: 0 };
  };

  const isSaving = createCategory.isPending || updateCategory.isPending || deleteCategory.isPending;

  return (
    <BudCard
      title="Categories"
      actions={
        <BudButton size="sm" onClick={openAddDialog}>
          Add Category
        </BudButton>
      }
    >
      <>
        {categories.length === 0 ? (
          <Typography color="text.secondary" fontStyle="italic">
            No categories found
          </Typography>
        ) : (
          <div className="space-y-3">
            {GROUPS.map((group) => {
              const items = grouped.get(group.type);
              if (!items || items.length === 0) return null;
              return (
                <div key={group.type}>
                  <Typography variant="caption" color="text.secondary" className="mb-1 block">
                    {group.label}
                  </Typography>
                  <div className="flex flex-wrap gap-1">
                    {items.map((cat) => {
                      const usage = getUsage(cat.id);
                      const tooltipLabel = `Used in ${usage.transactions} transaction${usage.transactions === 1 ? '' : 's'} and ${usage.planEntries} budget plan entr${usage.planEntries === 1 ? 'y' : 'ies'}`;

                      return (
                        <Tooltip key={cat.id} title={tooltipLabel} arrow>
                          <span>
                            <BudBadge
                              label={`${cat.name} (${usage.total})`}
                              color={group.color}
                              variant="outline"
                              onClick={() => openEditDialog(cat)}
                              onDelete={() => setDeleteTarget(cat)}
                            />
                          </span>
                        </Tooltip>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </>

      <BudModal
        open={dialogOpen}
        onClose={closeDialog}
        title={dialogMode === 'add' ? 'Add Category' : 'Edit Category'}
        maxWidth="xs"
        disableBackdropClose={isSaving}
        actions={
          <BudModalActions
            onCancel={closeDialog}
            onConfirm={onSave}
            confirmLabel={dialogMode === 'add' ? 'Create' : 'Save'}
            isPending={isSaving}
          />
        }
      >
        <BudInput control={control} name="name" label="Name" autoFocus />
        <BudSelect
          control={control}
          name="categoryType"
          label="Type"
          options={CATEGORY_TYPE_OPTIONS}
        />
      </BudModal>

      <BudConfirmModal
        open={deleteTarget !== null}
        title="Delete Category"
        message={
          deleteTarget ? (
            <>
              Delete <strong>{deleteTarget.name}</strong>? It is used in{' '}
              {getUsage(deleteTarget.id).transactions} transactions and{' '}
              {getUsage(deleteTarget.id).planEntries} budget plan entries. This cannot be undone.
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
    </BudCard>
  );
};

export default CategoriesSection;
