import { useEffect } from 'react';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import Button from '@mui/material/Button';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import TextField from '@mui/material/TextField';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
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
  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<BudgetPlanFormData>({
    resolver: zodResolver(budgetPlanSchema),
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
      <DialogTitle>{mode === 'add' ? 'Add Budget Plan' : 'Edit Budget Plan'}</DialogTitle>
      <DialogContent className="space-y-4 pt-4">
        <div className="grid grid-cols-1 gap-4 pt-2 sm:grid-cols-2">
          <Controller
            name="name"
            control={control}
            render={({ field }) => (
              <TextField
                label="Plan Name"
                value={field.value}
                onChange={field.onChange}
                error={Boolean(errors.name)}
                helperText={errors.name?.message}
                fullWidth
                required
                className="sm:col-span-2"
              />
            )}
          />
          <Controller
            name="planMonth"
            control={control}
            render={({ field }) => (
              <TextField
                label="Plan Month"
                type="month"
                value={field.value}
                onChange={field.onChange}
                error={Boolean(errors.planMonth)}
                helperText={errors.planMonth?.message}
                fullWidth
                required
                slotProps={{ inputLabel: { shrink: true } }}
              />
            )}
          />
          <Controller
            name="netIncomeMonthly"
            control={control}
            render={({ field }) => (
              <TextField
                label="Net Monthly Income"
                type="number"
                inputProps={{ min: 0, step: '0.01' }}
                value={field.value || ''}
                onChange={(event) => field.onChange(Number(event.target.value))}
                error={Boolean(errors.netIncomeMonthly)}
                helperText={errors.netIncomeMonthly?.message}
                fullWidth
                required
              />
            )}
          />
        </div>

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
      </DialogContent>
      <DialogActions className="px-6 pb-4">
        {mode === 'edit' && onDelete && (
          <Button
            color="error"
            onClick={() => {
              if (window.confirm('Delete this budget plan and all of its lines?')) {
                void onDelete();
              }
            }}
            disabled={isSaving}
            className="mr-auto"
          >
            Delete Plan
          </Button>
        )}
        <Button onClick={onClose} disabled={isSaving}>
          Cancel
        </Button>
        <Button variant="contained" onClick={() => void submit()} disabled={isSaving}>
          {isSaving ? 'Saving...' : mode === 'add' ? 'Create Plan' : 'Save Changes'}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

export default BudgetPlanDialog;
