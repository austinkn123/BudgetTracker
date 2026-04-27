import { useMemo, useState } from 'react';
import Button from '@mui/material/Button';
import ButtonGroup from '@mui/material/ButtonGroup';
import Card from '@mui/material/Card';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import ToggleButton from '@mui/material/ToggleButton';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import Typography from '@mui/material/Typography';
import { Plus } from 'lucide-react';
import { useTransactions } from './hooks/useTransactions';
import { useTransactionForm } from './hooks/useTransactionForm';
import { useCategories } from '../categories/hooks/useCategories';
import TransactionDialog from './components/TransactionDialog';
import TransactionTable from './components/TransactionTable';
import {
  getRangeValuesForPreset,
  groupTransactionsByPeriod,
  type TransactionGroupBy,
  type TransactionRangePreset,
} from './utils/transactionGroups';

type TransactionsSectionProps = {
  isLoading: boolean;
  setStatusMessage: (msg: string | null) => void;
  setStatusError: (msg: string | null) => void;
};

const TransactionsSection = ({
  isLoading,
  setStatusMessage,
  setStatusError,
}: TransactionsSectionProps) => {
  const { data: transactions = [] } = useTransactions();
  const { data: categories = [] } = useCategories();
  const [groupBy, setGroupBy] = useState<TransactionGroupBy>('month');
  const [rangePreset, setRangePreset] = useState<TransactionRangePreset>('this-month');
  const [rangeValues, setRangeValues] = useState(() => getRangeValuesForPreset('this-month'));

  const expenseCategories = useMemo(
    () => categories.filter((c) => c.categoryType === 'Expense' || c.categoryType === 'Both'),
    [categories],
  );

  const form = useTransactionForm(transactions, expenseCategories, setStatusMessage, setStatusError);

  const groupedResult = useMemo(
    () =>
      groupTransactionsByPeriod(transactions, {
        groupBy,
        startValue: groupBy === 'month' ? rangeValues.startMonth : rangeValues.startDate,
        endValue: groupBy === 'month' ? rangeValues.endMonth : rangeValues.endDate,
      }),
    [groupBy, rangeValues.endDate, rangeValues.endMonth, rangeValues.startDate, rangeValues.startMonth, transactions],
  );

  const handleGroupByChange = (_event: React.MouseEvent<HTMLElement>, value: TransactionGroupBy | null) => {
    if (value) {
      setGroupBy(value);
    }
  };

  const applyPreset = (preset: Exclude<TransactionRangePreset, 'custom'>) => {
    setRangePreset(preset);
    setRangeValues(getRangeValuesForPreset(preset));
  };

  const handleDateFieldChange =
    (field: 'startDate' | 'endDate' | 'startMonth' | 'endMonth') =>
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setRangePreset('custom');
      setRangeValues((current) => ({
        ...current,
        [field]: event.target.value,
      }));
    };

  if (isLoading) return null;

  return (
    <>
      <Stack spacing={2.5}>
        <div className="flex items-center justify-between gap-3">
          <div>
            <Typography variant="body2" color="text.secondary">
              Showing {groupedResult.visibleCount} of {transactions.length}{' '}
              {transactions.length === 1 ? 'transaction' : 'transactions'}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Organize your ledger by {groupBy} and narrow the visible period.
            </Typography>
          </div>
          <Button variant="contained" startIcon={<Plus className="w-4 h-4" />} onClick={form.openForAdd}>
            Add Transaction
          </Button>
        </div>

        <Card variant="outlined" sx={{ p: 2 }}>
          <Stack spacing={2}>
            <Stack
              direction={{ xs: 'column', lg: 'row' }}
              spacing={2}
              justifyContent="space-between"
              alignItems={{ xs: 'stretch', lg: 'center' }}
            >
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} alignItems={{ sm: 'center' }}>
                <Typography variant="subtitle2" sx={{ minWidth: 84 }}>
                  Group By
                </Typography>
                <ToggleButtonGroup
                  exclusive
                  color="primary"
                  value={groupBy}
                  onChange={handleGroupByChange}
                  size="small"
                >
                  <ToggleButton value="day">Day</ToggleButton>
                  <ToggleButton value="month">Month</ToggleButton>
                </ToggleButtonGroup>
              </Stack>

              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} alignItems={{ sm: 'center' }}>
                <Typography variant="subtitle2" sx={{ minWidth: 84 }}>
                  Quick Range
                </Typography>
                <ButtonGroup size="small" variant="outlined">
                  <Button
                    variant={rangePreset === 'this-month' ? 'contained' : 'outlined'}
                    onClick={() => applyPreset('this-month')}
                  >
                    This Month
                  </Button>
                  <Button
                    variant={rangePreset === 'last-3-months' ? 'contained' : 'outlined'}
                    onClick={() => applyPreset('last-3-months')}
                  >
                    Last 3 Months
                  </Button>
                  <Button
                    variant={rangePreset === 'ytd' ? 'contained' : 'outlined'}
                    onClick={() => applyPreset('ytd')}
                  >
                    Year to Date
                  </Button>
                  <Button
                    variant={rangePreset === 'all' ? 'contained' : 'outlined'}
                    onClick={() => applyPreset('all')}
                  >
                    All
                  </Button>
                </ButtonGroup>
              </Stack>
            </Stack>

            <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
              {groupBy === 'month' ? (
                <>
                  <TextField
                    label="Start Month"
                    type="month"
                    value={rangeValues.startMonth}
                    onChange={handleDateFieldChange('startMonth')}
                    fullWidth
                    slotProps={{ inputLabel: { shrink: true } }}
                  />
                  <TextField
                    label="End Month"
                    type="month"
                    value={rangeValues.endMonth}
                    onChange={handleDateFieldChange('endMonth')}
                    fullWidth
                    slotProps={{ inputLabel: { shrink: true } }}
                  />
                </>
              ) : (
                <>
                  <TextField
                    label="Start Date"
                    type="date"
                    value={rangeValues.startDate}
                    onChange={handleDateFieldChange('startDate')}
                    fullWidth
                    slotProps={{ inputLabel: { shrink: true } }}
                  />
                  <TextField
                    label="End Date"
                    type="date"
                    value={rangeValues.endDate}
                    onChange={handleDateFieldChange('endDate')}
                    fullWidth
                    slotProps={{ inputLabel: { shrink: true } }}
                  />
                </>
              )}
            </Stack>
          </Stack>
        </Card>

        <Card variant="outlined">
          <TransactionTable
            groups={groupedResult.groups}
            categories={categories}
            onRowClick={form.openForEdit}
          />
        </Card>
      </Stack>

      <TransactionDialog
        open={form.dialogOpen}
        mode={form.dialogMode}
        initialValues={form.initialValues}
        categories={expenseCategories}
        isSaving={form.isSaving}
        onClose={form.closeDialog}
        onSave={form.save}
        onDelete={form.deleteTransaction}
      />
    </>
  );
};

export default TransactionsSection;
