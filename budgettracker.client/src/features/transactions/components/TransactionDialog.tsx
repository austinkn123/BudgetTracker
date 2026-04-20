import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
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
  onClose,
  onSave,
  onDelete,
}: TransactionDialogProps) => {
  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<TransactionFormData>({
    resolver: zodResolver(transactionSchema),
    defaultValues: initialValues,
  });

  useEffect(() => {
    if (open) {
      reset(initialValues);
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
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{mode === 'add' ? 'Add Transaction' : 'Edit Transaction'}</DialogTitle>
      <DialogContent className="space-y-4 pt-4">
        <div className="grid grid-cols-2 gap-4 pt-2">
          <Controller
            name="amount"
            control={control}
            render={({ field }) => (
              <TextField
                label="Amount"
                type="number"
                inputProps={{ min: 0, step: '0.01' }}
                value={field.value || ''}
                onChange={(e) => field.onChange(Number(e.target.value))}
                error={Boolean(errors.amount)}
                helperText={errors.amount?.message}
                fullWidth
                required
              />
            )}
          />
          <Controller
            name="categoryId"
            control={control}
            render={({ field }) => (
              <TextField
                label="Category"
                select
                value={field.value || ''}
                onChange={(e) => field.onChange(Number(e.target.value))}
                error={Boolean(errors.categoryId)}
                helperText={errors.categoryId?.message}
                fullWidth
                required
              >
                {categories.map((cat) => (
                  <MenuItem key={cat.id} value={cat.id}>
                    {cat.name}
                  </MenuItem>
                ))}
              </TextField>
            )}
          />
          <Controller
            name="occurredAt"
            control={control}
            render={({ field }) => (
              <TextField
                label="Date"
                type="date"
                value={field.value}
                onChange={field.onChange}
                error={Boolean(errors.occurredAt)}
                helperText={errors.occurredAt?.message}
                fullWidth
                required
                slotProps={{ inputLabel: { shrink: true } }}
              />
            )}
          />
          <Controller
            name="payee"
            control={control}
            render={({ field }) => (
              <TextField
                label="Payee"
                value={field.value}
                onChange={field.onChange}
                error={Boolean(errors.payee)}
                helperText={errors.payee?.message}
                fullWidth
              />
            )}
          />
        </div>
        <Controller
          name="notes"
          control={control}
          render={({ field }) => (
            <TextField
              label="Notes"
              value={field.value}
              onChange={field.onChange}
              error={Boolean(errors.notes)}
              helperText={errors.notes?.message}
              fullWidth
              multiline
              rows={2}
            />
          )}
        />
      </DialogContent>
      <DialogActions className="px-6 pb-4">
        {mode === 'edit' && onDelete && (
          <Button color="error" onClick={onDelete} disabled={isSaving} className="mr-auto">
            Delete
          </Button>
        )}
        <Button onClick={onClose} disabled={isSaving}>
          Cancel
        </Button>
        <Button variant="contained" onClick={submit} disabled={isSaving}>
          {isSaving ? 'Saving...' : mode === 'add' ? 'Add' : 'Save'}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

export default TransactionDialog;
