import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import type { Category } from '../../../shared/types/api';

export type TransactionFormData = {
  accountId: number;
  categoryId: number;
  amount: number;
  occurredAt: string;
  payee: string;
  notes: string;
};

type TransactionDialogProps = {
  open: boolean;
  mode: 'add' | 'edit';
  formData: TransactionFormData;
  categories: Category[];
  isSaving: boolean;
  onClose: () => void;
  onChange: (field: keyof TransactionFormData, value: string | number) => void;
  onSave: () => void;
  onDelete?: () => void;
};

const TransactionDialog = ({
  open,
  mode,
  formData,
  categories,
  isSaving,
  onClose,
  onChange,
  onSave,
  onDelete,
}: TransactionDialogProps) => {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{mode === 'add' ? 'Add Transaction' : 'Edit Transaction'}</DialogTitle>
      <DialogContent className="space-y-4 pt-4">
        <div className="grid grid-cols-2 gap-4 pt-2">
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
            label="Date"
            type="date"
            value={formData.occurredAt}
            onChange={(e) => onChange('occurredAt', e.target.value)}
            fullWidth
            required
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            label="Payee"
            value={formData.payee}
            onChange={(e) => onChange('payee', e.target.value)}
            fullWidth
          />
        </div>
        <TextField
          label="Notes"
          value={formData.notes}
          onChange={(e) => onChange('notes', e.target.value)}
          fullWidth
          multiline
          rows={2}
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

export default TransactionDialog;
