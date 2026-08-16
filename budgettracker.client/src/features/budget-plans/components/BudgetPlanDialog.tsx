import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  Checkbox,
  ConfirmModal,
  Input,
  Modal,
  ModalActions,
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
      <Modal
        open={open}
        onClose={onClose}
        title={mode === 'add' ? 'Add Budget Plan' : 'Edit Budget Plan'}
        maxWidth="sm"
        disableBackdropClose={isSaving}
        actions={
          <ModalActions
            onCancel={onClose}
            onConfirm={() => void submit()}
            confirmLabel={mode === 'add' ? 'Create Plan' : 'Save Changes'}
            onDelete={mode === 'edit' && onDelete ? () => setConfirmDelete(true) : undefined}
            deleteLabel="Delete Plan"
            isPending={isSaving}
          />
        }
      >
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Input
            control={control}
            name="name"
            label="Plan Name"
            required
            className="sm:col-span-2"
          />
          <Input control={control} name="planMonth" label="Plan Month" type="month" required />
          <Input
            control={control}
            name="netIncomeMonthly"
            label="Net Monthly Income"
            type="number"
            valueAs="number"
            min={0}
            step="0.01"
            required
          />
        </div>

        <Checkbox control={control} name="isActive" label="Set as active budget plan" />
      </Modal>

      <ConfirmModal
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
