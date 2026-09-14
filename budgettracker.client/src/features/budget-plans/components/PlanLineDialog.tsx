import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useMemo } from 'react';
import {
  Checkbox,
  Input,
  Modal,
  ModalActions,
  Select,
} from '../../../shared/components/ui';
import type { Category } from '../../../shared/types/api';
import {
  planLineSchema,
  type PlanLineFormData,
} from '../../../shared/validation/planLineSchema';

export type { PlanLineFormData };

const BUCKET_OPTIONS = [
  { value: 'Core', label: 'Core' },
  { value: 'Buffer', label: 'Buffer' },
] as const;

const CADENCE_OPTIONS = [
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Annual', label: 'Annual' },
] as const;

type PlanLineDialogProps = {
  open: boolean;
  mode: 'add' | 'edit';
  initialValues: PlanLineFormData;
  categories: Category[];
  isSaving: boolean;
  onClose: () => void;
  onSave: (values: PlanLineFormData) => Promise<void> | void;
  onDelete?: () => void;
};

const PlanLineDialog = ({
  open,
  mode,
  initialValues,
  categories,
  isSaving,
  onClose,
  onSave,
  onDelete,
}: PlanLineDialogProps) => {
  const { control, handleSubmit, reset } = useForm<PlanLineFormData>({
    resolver: zodResolver(planLineSchema),
    defaultValues: initialValues,
  });

  useEffect(() => {
    if (open) {
      reset(initialValues);
    }
  }, [open, initialValues, reset]);

  const categoryOptions = useMemo(
    () => categories.map((cat) => ({ value: cat.id, label: cat.name })),
    [categories],
  );

  const submit = handleSubmit(async (values) => {
    try {
      await onSave(values);
    } catch {
      // Parent hook handles status messaging.
    }
  });

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={mode === 'add' ? 'Add Plan Line' : 'Edit Plan Line'}
      maxWidth="sm"
      disableBackdropClose={isSaving}
      actions={
        <ModalActions
          onCancel={onClose}
          onConfirm={submit}
          confirmLabel={mode === 'add' ? 'Add' : 'Save'}
          onDelete={mode === 'edit' && onDelete ? onDelete : undefined}
          isPending={isSaving}
        />
      }
    >
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Select
          control={control}
          name="categoryId"
          label="Category"
          options={categoryOptions}
          valueAs="number"
          required
        />
        <Input
          control={control}
          name="amount"
          label="Amount"
          type="number"
          valueAs="number"
          min={0}
          step="0.01"
          required
        />
        <Select control={control} name="bucket" label="Bucket" options={BUCKET_OPTIONS} />
        <Select control={control} name="cadence" label="Cadence" options={CADENCE_OPTIONS} />
      </div>

      <Input control={control} name="notes" label="Notes" multiline rows={2} />

      <Checkbox control={control} name="isStressFactor" label="Stress Factor" />
    </Modal>
  );
};

export default PlanLineDialog;
