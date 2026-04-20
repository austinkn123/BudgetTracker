import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import FormControlLabel from '@mui/material/FormControlLabel';
import Checkbox from '@mui/material/Checkbox';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import type { Category } from '../../../shared/types/api';
import {
  planLineSchema,
  type PlanLineFormData,
} from '../../../shared/validation/planLineSchema';

export type { PlanLineFormData };

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
  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<PlanLineFormData>({
    resolver: zodResolver(planLineSchema),
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
      <DialogTitle>{mode === 'add' ? 'Add Plan Line' : 'Edit Plan Line'}</DialogTitle>
      <DialogContent className="space-y-4 pt-4">
        <div className="grid grid-cols-2 gap-4 pt-2">
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
            name="bucket"
            control={control}
            render={({ field }) => (
              <TextField
                label="Bucket"
                select
                value={field.value}
                onChange={field.onChange}
                error={Boolean(errors.bucket)}
                helperText={errors.bucket?.message}
                fullWidth
              >
                <MenuItem value="Core">Core</MenuItem>
                <MenuItem value="Buffer">Buffer</MenuItem>
              </TextField>
            )}
          />
          <Controller
            name="cadence"
            control={control}
            render={({ field }) => (
              <TextField
                label="Cadence"
                select
                value={field.value}
                onChange={field.onChange}
                error={Boolean(errors.cadence)}
                helperText={errors.cadence?.message}
                fullWidth
              >
                <MenuItem value="Monthly">Monthly</MenuItem>
                <MenuItem value="Annual">Annual</MenuItem>
              </TextField>
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
        <Controller
          name="isStressFactor"
          control={control}
          render={({ field }) => (
            <FormControlLabel
              control={
                <Checkbox
                  checked={field.value}
                  onChange={(e) => field.onChange(e.target.checked)}
                />
              }
              label="Stress Factor"
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

export default PlanLineDialog;
