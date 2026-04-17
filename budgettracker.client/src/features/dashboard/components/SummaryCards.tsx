import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { TrendingUp, TrendingDown, Wallet } from 'lucide-react';
import type { SummaryTotals } from '../utils/chartHelpers';

interface SummaryCardsProps {
  totals: SummaryTotals;
}

const formatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

export function SummaryCards({ totals }: SummaryCardsProps) {
  const cards = [
    {
      label: 'Total Income',
      value: totals.totalIncome,
      icon: TrendingUp,
      color: 'text-green-600',
      bg: 'bg-green-50',
      iconBg: 'bg-green-100',
    },
    {
      label: 'Total Expenses',
      value: totals.totalExpenses,
      icon: TrendingDown,
      color: 'text-red-600',
      bg: 'bg-red-50',
      iconBg: 'bg-red-100',
    },
    {
      label: 'Net Balance',
      value: totals.netBalance,
      icon: Wallet,
      color: totals.netBalance >= 0 ? 'text-blue-600' : 'text-red-600',
      bg: totals.netBalance >= 0 ? 'bg-blue-50' : 'bg-red-50',
      iconBg: totals.netBalance >= 0 ? 'bg-blue-100' : 'bg-red-100',
    },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
      {cards.map((card) => (
        <Card key={card.label} className={card.bg}>
          <CardContent className="flex items-center gap-4 p-6">
            <div className={`${card.iconBg} rounded-full p-3`}>
              <card.icon className={`w-6 h-6 ${card.color}`} />
            </div>
            <div>
              <Typography variant="body2" className="text-gray-500 font-medium">
                {card.label}
              </Typography>
              <Typography variant="h5" className={`font-bold ${card.color}`}>
                {formatter.format(card.value)}
              </Typography>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
