import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Divider from '@mui/material/Divider';
import { format, parseISO } from 'date-fns';
import type { Transaction, Category } from '../../../shared/types/api';

interface RecentTransactionsProps {
  transactions: Transaction[];
  categories: Category[];
}

const formatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
});

export function RecentTransactions({ transactions, categories }: RecentTransactionsProps) {
  const categoryMap = new Map(categories.map((c) => [c.id, c.name]));

  const recent = [...transactions]
    .sort((a, b) => b.occurredAt.localeCompare(a.occurredAt))
    .slice(0, 5);

  return (
    <Card className="h-full rounded-2xl border border-slate-200/80 bg-white shadow-sm" elevation={0}>
      <CardContent className="p-6">
        <Typography variant="h6" className="font-semibold mb-1 text-slate-900">
          Recent Transactions
        </Typography>
        <Typography variant="body2" className="mb-3 text-slate-500">
          Your five latest entries
        </Typography>
        {recent.length === 0 ? (
          <Typography variant="body2" className="py-8 text-center text-slate-500">
            No transactions yet
          </Typography>
        ) : (
          <List disablePadding>
            {recent.map((t, i) => (
              <div key={t.id}>
                {i > 0 && <Divider className="border-slate-100" />}
                <ListItem disableGutters className="py-2">
                  <ListItemText
                    primary={t.payee || categoryMap.get(t.categoryId) || 'Transaction'}
                    secondary={`${format(parseISO(t.occurredAt), 'MMM d, yyyy')} · ${categoryMap.get(t.categoryId) ?? ''}`}
                    primaryTypographyProps={{ variant: 'body2', fontWeight: 600, color: '#0f172a' }}
                    secondaryTypographyProps={{ variant: 'caption', color: '#64748b' }}
                  />
                  <Typography
                    variant="body2"
                    className={`font-semibold ${t.transactionType === 'Income' ? 'text-green-600' : 'text-red-600'}`}
                  >
                    {t.transactionType === 'Income' ? '+' : '-'}
                    {formatter.format(t.amount)}
                  </Typography>
                </ListItem>
              </div>
            ))}
          </List>
        )}
      </CardContent>
    </Card>
  );
}
