import Box from '@mui/material/Box';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useMemo } from 'react';
import {
  BudAlert,
  BudInput,
  BudModal,
  BudModalActions,
  BudSelect,
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
    <BudModal
      open={open}
      onClose={onClose}
      title={mode === 'add' ? 'Add Transaction' : 'Edit Transaction'}
      maxWidth="sm"
      disableBackdropClose={isSaving}
      actions={
        <BudModalActions
          onCancel={onClose}
          onConfirm={submit}
          confirmLabel={mode === 'add' ? 'Add' : 'Save'}
          onDelete={mode === 'edit' && !locked && onDelete ? onDelete : undefined}
          isPending={isSaving}
        />
      }
    >
      {locked && (
        <BudAlert
          severity="info"
          variant="outlined"
          message="Imported from your bank — only category & notes can be edited."
        />
      )}

      {/* Single column on phones so the pair never overflows a 375px viewport. */}
      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' },
          gap: 2,
        }}
      >
        <BudInput
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
        <BudSelect
          control={control}
          name="categoryId"
          label="Category"
          options={categoryOptions}
          valueAs="number"
          required
        />
        <BudInput
          control={control}
          name="occurredAt"
          label="Date"
          type="date"
          disabled={locked}
          required
        />
        <BudInput control={control} name="payee" label="Payee" disabled={locked} />
      </Box>

      <BudInput control={control} name="notes" label="Notes" multiline rows={2} />
    </BudModal>
  );
};

export default TransactionDialog;
