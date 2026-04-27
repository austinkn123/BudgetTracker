import { Fragment } from 'react';
import { format, parseISO } from 'date-fns';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import type { Category, Transaction } from '../../../shared/types/api';
import type { TransactionGroup } from '../utils/transactionGroups';

type TransactionTableProps = {
  groups: TransactionGroup[];
  categories: Category[];
  onRowClick: (transaction: Transaction) => void;
};

const TransactionTable = ({ groups, categories, onRowClick }: TransactionTableProps) => {
  const categoryMap = new Map(categories.map((c) => [c.id, c.name]));

  return (
    <TableContainer>
      <Table>
        <TableHead>
          <TableRow>
            <TableCell>Date</TableCell>
            <TableCell>Payee</TableCell>
            <TableCell>Category</TableCell>
            <TableCell align="right">Amount</TableCell>
            <TableCell>Notes</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {groups.length > 0 ? (
            groups.map((group) => (
              <Fragment key={group.key}>
                <TableRow>
                  <TableCell colSpan={5} sx={{ backgroundColor: 'grey.50', py: 2 }}>
                    <Stack
                      direction={{ xs: 'column', md: 'row' }}
                      justifyContent="space-between"
                      spacing={1.5}
                    >
                      <Box>
                        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                          {group.label}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {group.transactionCount}{' '}
                          {group.transactionCount === 1 ? 'transaction' : 'transactions'}
                        </Typography>
                      </Box>
                      <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
                        <Chip
                          size="small"
                          variant="outlined"
                          color="success"
                          label={`Income $${group.incomeTotal.toFixed(2)}`}
                        />
                        <Chip
                          size="small"
                          variant="outlined"
                          color="error"
                          label={`Outflow $${group.outflowTotal.toFixed(2)}`}
                        />
                        <Chip
                          size="small"
                          variant="filled"
                          color={group.netTotal >= 0 ? 'success' : 'error'}
                          label={`Net ${group.netTotal >= 0 ? '+' : '-'}$${Math.abs(group.netTotal).toFixed(2)}`}
                        />
                      </Stack>
                    </Stack>
                  </TableCell>
                </TableRow>
                {group.transactions.map((transaction) => (
                  <TableRow
                    key={transaction.id}
                    hover
                    onClick={() => onRowClick(transaction)}
                    className="cursor-pointer"
                  >
                    <TableCell>{format(parseISO(transaction.occurredAt), 'PP')}</TableCell>
                    <TableCell>{transaction.payee || '-'}</TableCell>
                    <TableCell>{categoryMap.get(transaction.categoryId) ?? '-'}</TableCell>
                    <TableCell
                      align="right"
                      sx={{
                        color: transaction.transactionType === 'Income' ? 'success.main' : 'error.main',
                        fontWeight: 600,
                      }}
                    >
                      {transaction.transactionType === 'Income' ? '+' : '-'}${transaction.amount.toFixed(2)}
                    </TableCell>
                    <TableCell className="max-w-xs truncate">{transaction.notes || '-'}</TableCell>
                  </TableRow>
                ))}
              </Fragment>
            ))
          ) : (
            <TableRow>
              <TableCell colSpan={5} align="center" className="py-8">
                <Typography color="text.secondary" fontStyle="italic">
                  No transactions in this range
                </Typography>
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
};

export default TransactionTable;
