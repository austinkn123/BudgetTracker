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
    <Card className="h-full">
      <CardContent>
        <Typography variant="h6" className="font-semibold mb-2">
          Recent Transactions
        </Typography>
        {recent.length === 0 ? (
          <Typography variant="body2" color="text.secondary" className="py-8 text-center">
            No transactions yet
          </Typography>
        ) : (
          <List disablePadding>
            {recent.map((t, i) => (
              <div key={t.id}>
                {i > 0 && <Divider />}
                <ListItem disableGutters className="py-2">
                  <ListItemText
                    primary={t.payee || categoryMap.get(t.categoryId) || 'Transaction'}
                    secondary={`${format(parseISO(t.occurredAt), 'MMM d, yyyy')} · ${categoryMap.get(t.categoryId) ?? ''}`}
                    primaryTypographyProps={{ variant: 'body2', fontWeight: 600 }}
                    secondaryTypographyProps={{ variant: 'caption' }}
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
