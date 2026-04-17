import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import FormControlLabel from '@mui/material/FormControlLabel';
import Checkbox from '@mui/material/Checkbox';
import type { Category } from '../../../shared/types/api';

export type PlanLineFormData = {
  categoryId: number;
  bucket: 'Core' | 'Buffer';
  cadence: 'Monthly' | 'Annual';
  amount: number;
  isStressFactor: boolean;
  notes: string;
};

type PlanLineDialogProps = {
  open: boolean;
  mode: 'add' | 'edit';
  formData: PlanLineFormData;
  categories: Category[];
  isSaving: boolean;
  onClose: () => void;
  onChange: (field: keyof PlanLineFormData, value: string | number | boolean) => void;
  onSave: () => void;
  onDelete?: () => void;
};

const PlanLineDialog = ({
  open,
  mode,
  formData,
  categories,
  isSaving,
  onClose,
  onChange,
  onSave,
  onDelete,
}: PlanLineDialogProps) => {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{mode === 'add' ? 'Add Plan Line' : 'Edit Plan Line'}</DialogTitle>
      <DialogContent className="space-y-4 pt-4">
        <div className="grid grid-cols-2 gap-4 pt-2">
          <TextField
            label="Category"
            select
            value={formData.categoryId || ''}
            onChange={(e) => onChange('categoryId', Number(e.target.value))}
            fullWidth
            required
          >
            {categories.map((cat) => (
              <MenuItem key={cat.id} value={cat.id}>
                {cat.name}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label="Amount"
            type="number"
            inputProps={{ min: 0, step: '0.01' }}
            value={formData.amount || ''}
            onChange={(e) => onChange('amount', Number(e.target.value))}
            fullWidth
            required
          />
          <TextField
            label="Bucket"
            select
            value={formData.bucket}
            onChange={(e) => onChange('bucket', e.target.value)}
            fullWidth
          >
            <MenuItem value="Core">Core</MenuItem>
            <MenuItem value="Buffer">Buffer</MenuItem>
          </TextField>
          <TextField
            label="Cadence"
            select
            value={formData.cadence}
            onChange={(e) => onChange('cadence', e.target.value)}
            fullWidth
          >
            <MenuItem value="Monthly">Monthly</MenuItem>
            <MenuItem value="Annual">Annual</MenuItem>
          </TextField>
        </div>
        <TextField
          label="Notes"
          value={formData.notes}
          onChange={(e) => onChange('notes', e.target.value)}
          fullWidth
          multiline
          rows={2}
        />
        <FormControlLabel
          control={
            <Checkbox
              checked={formData.isStressFactor}
              onChange={(e) => onChange('isStressFactor', e.target.checked)}
            />
          }
          label="Stress Factor"
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
        <Button variant="contained" onClick={onSave} disabled={isSaving}>
          {isSaving ? 'Saving...' : mode === 'add' ? 'Add' : 'Save'}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

export default PlanLineDialog;
