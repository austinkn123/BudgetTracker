import { format } from 'date-fns';
import { Card } from '../../../shared/components/ui';
import type { UpcomingItem } from '../../../shared/types/api';
import { currency } from '../../../shared/utils/format';
import { cn } from '../../../shared/utils/cn';
import { timeline } from '../utils/moneyMap';

interface BillsAheadProps {
  upcoming: UpcomingItem[];
  /** Monthly set-aside that covers every yearly bill in the plan. */
  setAside: number;
  today: Date;
}

/** A year of lump-sum bills and extra paychecks, with what to set aside each month so none is a surprise. */
const BillsAhead = ({ upcoming, setAside, today }: BillsAheadProps) => {
  const months = timeline(upcoming, today);

  return (
    <Card
      title="Big bills ahead"
      subtitle="Projected from when you last paid each yearly bill in the plan."
      actions={
        setAside > 0 && (
          <div className="rounded-lg bg-warning-subtle px-4 py-2 text-right">
            <p className="numeric text-xl font-bold text-ink">{currency.format(setAside)}/mo</p>
            <p className="text-xs text-warning-dark">into a bills fund covers them</p>
          </div>
        )
      }
    >
      {upcoming.length === 0 ? (
        <p className="text-sm text-ink-muted">
          Add yearly bills (insurance, HOA, registration) to your plan as Annual lines and they will show here.
        </p>
      ) : (
        <ol className="grid grid-cols-4 gap-y-6 sm:grid-cols-6 lg:grid-cols-12" aria-label="Next 12 months">
          {months.map(({ month, items }) => (
            <li key={month.toISOString()} className="flex flex-col items-center gap-2 px-1">
              <span className={cn('text-xs', items.length ? 'font-semibold text-ink' : 'text-ink-muted')}>
                {format(month, month.getMonth() === 0 || month === months[0].month ? 'MMM ’yy' : 'MMM')}
              </span>
              <span className="h-1 w-full rounded-full bg-border-subtle" aria-hidden />
              {items.map((item) => (
                <div
                  key={`${item.kind}-${item.label}-${item.date}`}
                  className={cn(
                    'w-full rounded-md border px-2 py-1.5',
                    item.kind === 'Bill' ? 'border-warning/50 bg-warning-subtle' : 'border-success/50 bg-success-subtle',
                  )}
                >
                  <p className={cn('text-2xs leading-tight', item.kind === 'Bill' ? 'text-warning-dark' : 'text-success-dark')}>
                    {item.label} · {format(new Date(item.date), 'MMM d')}
                  </p>
                  <p className="numeric text-sm font-semibold text-ink">
                    {item.kind === 'ExtraPaycheck' ? '+' : ''}
                    {currency.format(item.amount)}
                  </p>
                </div>
              ))}
            </li>
          ))}
        </ol>
      )}
    </Card>
  );
};

export default BillsAhead;
