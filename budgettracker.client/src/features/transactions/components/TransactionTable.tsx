import { format, startOfDay } from 'date-fns';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import { alpha, type SxProps, type Theme } from '@mui/material/styles';
import Typography from '@mui/material/Typography';
import { AdapterDateFns } from '@mui/x-date-pickers/AdapterDateFns';
import {
  DateCalendar,
  LocalizationProvider,
  PickerDay,
  type PickerDayProps,
} from '@mui/x-date-pickers';
import type { Category, Transaction } from '../../../shared/types/api';
import {
  toDateKey,
  type TransactionDaySummary,
} from '../utils/transactionGroups';

type TransactionTableProps = {
  categories: Category[];
  daySummaries: Map<string, TransactionDaySummary>;
  selectedDate: Date;
  selectedDaySummary: TransactionDaySummary | null;
  onDateChange: (date: Date) => void;
  onMonthChange: (month: Date) => void;
  onAddTransaction: (occurredAt?: string) => void;
  onRowClick: (transaction: Transaction) => void;
};

const getHighlightedDaySx = (netTotal: number): SxProps<Theme> => (theme) => {
  const tone = netTotal >= 0 ? theme.palette.success : theme.palette.error;

  return {
    fontWeight: 700,
    color: tone.dark,
    bgcolor: alpha(tone.main, 0.14),
    border: `1px solid ${alpha(tone.main, 0.32)}`,
    '&:hover, &:focus': {
      bgcolor: alpha(tone.main, 0.22),
    },
    '&.Mui-selected': {
      bgcolor: tone.main,
      color: theme.palette.common.white,
      '&:hover, &:focus': {
        bgcolor: tone.dark,
      },
    },
  };
};

const TransactionTable = ({
  categories,
  daySummaries,
  selectedDate,
  selectedDaySummary,
  onDateChange,
  onMonthChange,
  onAddTransaction,
  onRowClick,
}: TransactionTableProps) => {
  const categoryMap = new Map(categories.map((c) => [c.id, c.name]));
  const selectedDateKey = toDateKey(selectedDate);
  const selectedTransactions = selectedDaySummary?.transactions ?? [];
  const selectedIncomeTotal = selectedDaySummary?.incomeTotal ?? 0;
  const selectedOutflowTotal = selectedDaySummary?.outflowTotal ?? 0;
  const selectedNetTotal = selectedDaySummary?.netTotal ?? 0;

  const TransactionCalendarDay = (props: PickerDayProps) => {
    const dateKey = toDateKey(props.day);
    const summary = daySummaries.get(dateKey);

    return (
      <PickerDay
        {...props}
        sx={
          props.outsideCurrentMonth || !summary
            ? undefined
            : getHighlightedDaySx(summary.netTotal)
        }
      />
    );
  };

  return (
    <Stack direction={{ xs: 'column', lg: 'row' }} spacing={0} alignItems="stretch">
      <Box
        sx={{
          order: { xs: 1, lg: 2 },
          p: 2.5,
          pb: 2,
          flex: 1,
          minWidth: 0,
          borderBottom: { xs: '1px solid', lg: 'none' },
          borderLeft: { xs: 'none', lg: '1px solid' },
          borderColor: (theme) => alpha(theme.palette.divider, 0.35),
          backgroundColor: (theme) => alpha(theme.palette.grey[500], 0.03),
        }}
      >
        <Stack spacing={2}>
          <Box>
            <Typography variant="overline" color="text.secondary">
              Selected Day
            </Typography>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              {format(selectedDate, 'PPPP')}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {selectedTransactions.length > 0
                ? `Review ${selectedTransactions.length} ${selectedTransactions.length === 1 ? 'transaction' : 'transactions'} for this day.`
                : 'No transactions are recorded for this day yet.'}
            </Typography>
          </Box>

          <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
            <Chip size="small" label={`${selectedTransactions.length} item${selectedTransactions.length === 1 ? '' : 's'}`} />
            <Chip size="small" variant="outlined" color="success" label={`Income $${selectedIncomeTotal.toFixed(2)}`} />
            <Chip size="small" variant="outlined" color="error" label={`Outflow $${selectedOutflowTotal.toFixed(2)}`} />
            <Chip
              size="small"
              color={selectedNetTotal >= 0 ? 'success' : 'error'}
              label={`Net ${selectedNetTotal >= 0 ? '+' : '-'}$${Math.abs(selectedNetTotal).toFixed(2)}`}
            />
          </Stack>

          {selectedTransactions.length > 0 ? (
            <List disablePadding sx={{ border: '1px solid', borderColor: 'divider', borderRadius: 3, overflow: 'hidden' }}>
              {selectedTransactions.map((transaction, index) => (
                <Box key={transaction.id}>
                  {index > 0 && <Divider component="li" />}
                  <ListItemButton alignItems="flex-start" onClick={() => onRowClick(transaction)}>
                    <ListItemText
                      primary={transaction.payee || categoryMap.get(transaction.categoryId) || 'Uncategorized transaction'}
                      secondary={
                        [categoryMap.get(transaction.categoryId) ?? 'Uncategorized', transaction.notes || transaction.transactionType]
                          .filter(Boolean)
                          .join(' · ')
                      }
                    />
                    <Typography
                      variant="body2"
                      sx={{
                        color: transaction.transactionType === 'Income' ? 'success.main' : 'error.main',
                        fontWeight: 700,
                        ml: 2,
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {transaction.transactionType === 'Income' ? '+' : '-'}${transaction.amount.toFixed(2)}
                    </Typography>
                  </ListItemButton>
                </Box>
              ))}
            </List>
          ) : (
            <Box
              sx={{
                border: '1px dashed',
                borderColor: 'divider',
                borderRadius: 3,
                p: 3,
                textAlign: 'center',
                backgroundColor: 'grey.50',
              }}
            >
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                No transactions on {format(selectedDate, 'PP')}
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.75, mb: 2 }}>
                Choose another date on the calendar or add a transaction for this day.
              </Typography>
              <Button variant="contained" onClick={() => onAddTransaction(selectedDateKey)}>
                Add Transaction for This Day
              </Button>
            </Box>
          )}
        </Stack>
      </Box>

      <Box
        sx={{
          order: { xs: 2, lg: 1 },
          px: { xs: 1, md: 2 },
          py: 2,
          flexShrink: 0,
          display: 'flex',
          justifyContent: 'center',
        }}
      >
        <LocalizationProvider dateAdapter={AdapterDateFns}>
          <DateCalendar
            value={selectedDate}
            onChange={(value) => {
              if (value) {
                onDateChange(startOfDay(value));
              }
            }}
            onMonthChange={(month) => onMonthChange(startOfDay(month))}
            fixedWeekNumber={6}
            showDaysOutsideCurrentMonth
            slots={{ day: TransactionCalendarDay }}
          />
        </LocalizationProvider>
      </Box>
    </Stack>
  );
};

export default TransactionTable;
