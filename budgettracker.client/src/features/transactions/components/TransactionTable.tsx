import { format } from 'date-fns';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Typography from '@mui/material/Typography';
import type { Category, Transaction } from '../../../shared/types/api';

type TransactionTableProps = {
  transactions: Transaction[];
  categories: Category[];
  onRowClick: (transaction: Transaction) => void;
};

const TransactionTable = ({ transactions, categories, onRowClick }: TransactionTableProps) => {
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
          {transactions.length > 0 ? (
            transactions.map((transaction) => (
              <TableRow
                key={transaction.id}
                hover
                onClick={() => onRowClick(transaction)}
                className="cursor-pointer"
              >
                <TableCell>{format(new Date(transaction.occurredAt), 'PP')}</TableCell>
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
            ))
          ) : (
            <TableRow>
              <TableCell colSpan={5} align="center" className="py-8">
                <Typography color="text.secondary" fontStyle="italic">
                  No transactions found
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
