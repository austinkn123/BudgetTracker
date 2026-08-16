import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useMemo } from 'react';
import {
  Alert,
  Input,
  Modal,
  ModalActions,
  Select,
} from '../../../shared/components/ui';
import type { Category } from '../../../shared/types/api';
import {
  transactionSchema,
  type TransactionFormData,
} from '../../../shared/validation/transactionSchema';

export type { TransactionFormData };

type TransactionDialogProps = {
  open: boolean;
  mode: 'add' | 'edit';
  initialValues: TransactionFormData;
  categories: Category[];
  isSaving: boolean;
  locked?: boolean;
  onClose: () => void;
  onSave: (values: TransactionFormData) => Promise<void> | void;
  onDelete?: () => void;
};

const TransactionDialog = ({
  open,
  mode,
  initialValues,
  categories,
  isSaving,
  locked = false,
  onClose,
  onSave,
  onDelete,
}: TransactionDialogProps) => {
  const { control, handleSubmit, reset } = useForm<TransactionFormData>({
    resolver: zodResolver(transactionSchema),
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
      title={mode === 'add' ? 'Add Transaction' : 'Edit Transaction'}
      maxWidth="sm"
      disableBackdropClose={isSaving}
      actions={
        <ModalActions
          onCancel={onClose}
          onConfirm={submit}
          confirmLabel={mode === 'add' ? 'Add' : 'Save'}
          onDelete={mode === 'edit' && !locked && onDelete ? onDelete : undefined}
          isPending={isSaving}
        />
      }
    >
      {locked && (
        <Alert
          severity="info"
          variant="outlined"
          message="Imported from your bank — only category & notes can be edited."
        />
      )}

      {/* Single column on phones so the pair never overflows a 375px viewport. */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Input
          control={control}
          name="amount"
          label="Amount"
          type="number"
          valueAs="number"
          min={0}
          step="0.01"
          disabled={locked}
          required
        />
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
          name="occurredAt"
          label="Date"
          type="date"
          disabled={locked}
          required
        />
        <Input control={control} name="payee" label="Payee" disabled={locked} />
      </div>

      <Input control={control} name="notes" label="Notes" multiline rows={2} />
    </Modal>
  );
};

export default TransactionDialog;
