import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { TrendingUp, TrendingDown, Wallet } from 'lucide-react';
import type { SummaryTotals } from '../utils/chartHelpers';

interface SummaryCardsProps {
  totals: SummaryTotals;
  mode?: 'actual' | 'planned';
}

const formatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

export function SummaryCards({ totals, mode = 'actual' }: SummaryCardsProps) {
  const cards = [
    {
      label: mode === 'planned' ? 'Planned Income' : 'Total Income',
      value: totals.totalIncome,
      icon: TrendingUp,
      color: 'text-green-600',
      bg: 'from-emerald-50 to-white',
      iconBg: 'bg-emerald-100',
    },
    {
      label: mode === 'planned' ? 'Planned Expenses' : 'Total Expenses',
      value: totals.totalExpenses,
      icon: TrendingDown,
      color: 'text-red-600',
      bg: 'from-rose-50 to-white',
      iconBg: 'bg-rose-100',
    },
    {
      label: mode === 'planned' ? 'Monthly Net' : 'Net Balance',
      value: totals.netBalance,
      icon: Wallet,
      color: totals.netBalance >= 0 ? 'text-blue-600' : 'text-red-600',
      bg: totals.netBalance >= 0 ? 'from-blue-50 to-white' : 'from-rose-50 to-white',
      iconBg: totals.netBalance >= 0 ? 'bg-blue-100' : 'bg-rose-100',
    },
  ];

  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
      {cards.map((card) => (
        <Card
          key={card.label}
          className={`h-full rounded-2xl border border-slate-200/80 bg-gradient-to-br ${card.bg} shadow-sm transition-transform duration-200 hover:-translate-y-0.5 hover:shadow-md`}
          elevation={0}
        >
          <CardContent className="flex items-center gap-4 p-6">
            <div className={`${card.iconBg} rounded-xl p-3`}>
              <card.icon className={`w-6 h-6 ${card.color}`} />
            </div>
            <div className="min-w-0">
              <Typography variant="body2" className="text-slate-500 font-medium">
                {card.label}
              </Typography>
              <Typography variant="h5" className={`font-bold tracking-tight ${card.color}`}>
                {formatter.format(card.value)}
              </Typography>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
