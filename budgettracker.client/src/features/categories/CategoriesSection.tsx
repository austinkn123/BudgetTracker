import { useMemo } from 'react';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import Tooltip from '@mui/material/Tooltip';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
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
  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CategoryFormValues>({
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
    const map = new Map<number, { transactions: number; planLines: number; total: number }>();

    for (const category of categories) {
      map.set(category.id, { transactions: 0, planLines: 0, total: 0 });
    }

    for (const transaction of transactions) {
      const usage = map.get(transaction.categoryId) ?? { transactions: 0, planLines: 0, total: 0 };
      usage.transactions += 1;
      usage.total += 1;
      map.set(transaction.categoryId, usage);
    }

    for (const plan of budgetPlans) {
      for (const line of plan.lines) {
        if (line.categoryId == null) continue;
        const usage = map.get(line.categoryId) ?? { transactions: 0, planLines: 0, total: 0 };
        usage.planLines += 1;
        usage.total += 1;
        map.set(line.categoryId, usage);
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
    return usageByCategoryId.get(categoryId) ?? { transactions: 0, planLines: 0, total: 0 };
  };

  const isSaving = createCategory.isPending || updateCategory.isPending || deleteCategory.isPending;

  return (
    <Card variant="outlined">
      <CardContent>
        <div className="mb-3 flex items-center justify-between gap-3">
          <Typography variant="subtitle1" fontWeight={600}>
            Categories
          </Typography>
          <Button variant="contained" size="small" onClick={openAddDialog}>
            Add Category
          </Button>
        </div>
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
                      const tooltipLabel = `Used in ${usage.transactions} transaction${usage.transactions === 1 ? '' : 's'} and ${usage.planLines} budget plan line${usage.planLines === 1 ? '' : 's'}`;

                      return (
                        <Tooltip key={cat.id} title={tooltipLabel} arrow>
                          <Chip
                            label={`${cat.name} (${usage.total})`}
                            size="small"
                            color={group.color}
                            variant="outlined"
                            onClick={() => openEditDialog(cat)}
                            onDelete={() => setDeleteTarget(cat)}
                          />
                        </Tooltip>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </CardContent>

      <Dialog open={dialogOpen} onClose={closeDialog} maxWidth="xs" fullWidth>
        <DialogTitle>{dialogMode === 'add' ? 'Add Category' : 'Edit Category'}</DialogTitle>
        <DialogContent className="space-y-4 pt-4">
          <Controller
            name="name"
            control={control}
            render={({ field }) => (
              <TextField
                label="Name"
                fullWidth
                autoFocus
                error={Boolean(errors.name)}
                helperText={errors.name?.message}
                {...field}
              />
            )}
          />
          <Controller
            name="categoryType"
            control={control}
            render={({ field }) => (
              <TextField
                label="Type"
                select
                fullWidth
                error={Boolean(errors.categoryType)}
                helperText={errors.categoryType?.message}
                {...field}
              >
                <MenuItem value="Income">Income</MenuItem>
                <MenuItem value="Expense">Expense</MenuItem>
                <MenuItem value="Both">Both</MenuItem>
              </TextField>
            )}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={closeDialog} disabled={isSaving}>Cancel</Button>
          <Button onClick={onSave} variant="contained" disabled={isSaving}>
            {isSaving ? 'Saving...' : dialogMode === 'add' ? 'Create' : 'Save'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={deleteTarget !== null} onClose={() => setDeleteTarget(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Delete Category</DialogTitle>
        <DialogContent>
          {deleteTarget && (
            <Typography variant="caption" color="text.secondary" className="mb-2 block">
              Used in {getUsage(deleteTarget.id).transactions} transactions and {getUsage(deleteTarget.id).planLines} budget plan lines.
            </Typography>
          )}
          <Typography variant="body2">
            Delete {deleteTarget?.name}? This cannot be undone.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteTarget(null)} disabled={isSaving}>Cancel</Button>
          <Button color="error" onClick={onConfirmDelete} disabled={isSaving}>
            {isSaving ? 'Deleting...' : 'Delete'}
          </Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
};

export default CategoriesSection;
