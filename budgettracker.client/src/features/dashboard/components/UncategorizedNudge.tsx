import { Link } from 'react-router-dom';
import { Sparkles } from 'lucide-react';
import { Card } from '../../../shared/components/ui';

type UncategorizedNudgeProps = {
  count: number;
};

/**
 * The dashboard's numbers all group by category, so uncategorized rows are invisible to every
 * chart on this page. Nothing used to say so — this points at the work and links straight to
 * the filtered review list.
 */
const UncategorizedNudge = ({ count }: UncategorizedNudgeProps) => {
  if (count === 0) return null;

  return (
    <Card padding="sm">
      <div className="flex flex-col gap-2.5 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-start gap-2.5">
          <Sparkles size={16} className="mt-0.5 shrink-0 text-primary" />
          <div>
            <p className="text-sm font-semibold text-ink">
              {count} {count === 1 ? 'transaction needs' : 'transactions need'} a category
            </p>
            <p className="text-sm text-ink-muted">
              Uncategorized activity is missing from every figure on this page.
            </p>
          </div>
        </div>
        <Link
          to="/transactions"
          className="focus-ring shrink-0 rounded-md border border-border bg-surface px-3 py-1.5 text-sm font-medium text-ink shadow-xs transition-colors duration-120 hover:border-border-strong"
        >
          Review them
        </Link>
      </div>
    </Card>
  );
};

export default UncategorizedNudge;
