import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  BudConfirmModal,
  BudInput,
  BudModal,
  BudModalActions,
} from '../../../shared/components/ui';
import {
  budgetPlanSchema,
  type BudgetPlanFormData,
} from '../../../shared/validation/budgetPlanSchema';

export type { BudgetPlanFormData };

type BudgetPlanDialogProps = {
  open: boolean;
  mode: 'add' | 'edit';
  initialValues: BudgetPlanFormData;
  isSaving: boolean;
  onClose: () => void;
  onSave: (values: BudgetPlanFormData) => Promise<void> | void;
  onDelete?: () => Promise<void> | void;
};

const BudgetPlanDialog = ({
  open,
  mode,
  initialValues,
  isSaving,
  onClose,
  onSave,
  onDelete,
}: BudgetPlanDialogProps) => {
  const [confirmDelete, setConfirmDelete] = useState(false);

  const { control, handleSubmit, reset } = useForm<BudgetPlanFormData>({
    resolver: zodResolver(budgetPlanSchema),
    defaultValues: initialValues,
  });

  useEffect(() => {
    if (open) {
      reset(initialValues);
      setConfirmDelete(false);
    }
  }, [open, initialValues, reset]);

  const submit = handleSubmit(async (values) => {
    try {
      await onSave(values);
    } catch {
      // Parent hook handles status messaging.
    }
  });

  return (
    <>
      <BudModal
        open={open}
        onClose={onClose}
        title={mode === 'add' ? 'Add Budget Plan' : 'Edit Budget Plan'}
        maxWidth="sm"
        disableBackdropClose={isSaving}
        actions={
          <BudModalActions
            onCancel={onClose}
            onConfirm={() => void submit()}
            confirmLabel={mode === 'add' ? 'Create Plan' : 'Save Changes'}
            onDelete={mode === 'edit' && onDelete ? () => setConfirmDelete(true) : undefined}
            deleteLabel="Delete Plan"
            isPending={isSaving}
          />
        }
      >
        <Box
          sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2 }}
        >
          <BudInput
            control={control}
            name="name"
            label="Plan Name"
            required
            sx={{ gridColumn: { sm: '1 / -1' } }}
          />
          <BudInput control={control} name="planMonth" label="Plan Month" type="month" required />
          <BudInput
            control={control}
            name="netIncomeMonthly"
            label="Net Monthly Income"
            type="number"
            valueAs="number"
            min={0}
            step="0.01"
            required
          />
        </Box>

        <Controller
          name="isActive"
          control={control}
          render={({ field }) => (
            <FormControlLabel
              control={
                <Checkbox
                  checked={field.value}
                  onChange={(event) => field.onChange(event.target.checked)}
                />
              }
              label="Set as active budget plan"
            />
          )}
        />
      </BudModal>

      <BudConfirmModal
        open={confirmDelete}
        title="Delete budget plan?"
        message="This removes the plan and all of its lines. This cannot be undone."
        confirmLabel="Delete Plan"
        destructive
        isPending={isSaving}
        onCancel={() => setConfirmDelete(false)}
        onConfirm={() => {
          setConfirmDelete(false);
          void onDelete?.();
        }}
      />
    </>
  );
};

export default BudgetPlanDialog;
