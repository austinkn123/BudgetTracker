import { Card, Progress } from '../../../shared/components/ui';
import type { MoneyMapLine } from '../../../shared/types/api';
import { cssVar } from '../../../shared/theme/tokens';
import { currency } from '../../../shared/utils/format';
import { cn } from '../../../shared/utils/cn';
import { groupLines, lineStatus } from '../utils/moneyMap';
import type { Split, Tone } from '../utils/moneyMap';

const TONE_BAR: Record<Tone, string> = {
  success: cssVar('success'),
  warning: cssVar('warning'),
  info: cssVar('secondary'),
};
const TONE_TEXT: Record<Tone, string> = {
  success: 'text-success-dark',
  warning: 'text-warning-dark',
  info: 'text-secondary-dark',
};
const GROUP_DOT = { Core: 'bg-primary', Buffer: 'bg-secondary', Unplanned: 'bg-warning' } as const;

interface BudgetTableProps {
  lines: MoneyMapLine[];
  months: number;
  income: number;
  plan: Split;
  actual: Split;
}

/** Each plan line against its monthly average, grouped must-pay / flexible, attention first. */
const BudgetTable = ({ lines, months, income, plan, actual }: BudgetTableProps) => (
  <Card
    title={months === 1 ? 'The plan vs this month so far' : `The plan vs your last ${months} months`}
    subtitle="Monthly amounts. Yearly bills show the monthly set-aside. Lines over plan come first in their group."
    contentClassName="p-0"
  >
    {/* Native <table>: one <tbody> per group with a spanning group-header row; the shared Table renders a single flat body. */}
    <table className="w-full text-sm">
      <thead>
        <tr className="border-b border-border text-left text-2xs font-semibold uppercase tracking-[0.08em] text-ink-muted">
          <th className="px-5 py-2.5 font-semibold">Category</th>
          <th className="px-3 py-2.5 text-right font-semibold">{months === 1 ? 'Spent' : 'Avg / mo'}</th>
          <th className="px-3 py-2.5 text-right font-semibold">Plan / mo</th>
          <th className="hidden w-56 px-3 py-2.5 font-semibold md:table-cell">Avg as % of plan</th>
          <th className="px-5 py-2.5 font-semibold">Status</th>
        </tr>
      </thead>
      {groupLines(lines).map((group) => {
        const planned = group.lines.reduce((s, l) => s + l.planMonthly, 0);
        return (
          <tbody key={group.key}>
            <tr>
              <th colSpan={5} className="px-5 pb-1.5 pt-5 text-left">
                <span className="flex items-center gap-2 text-xs font-bold uppercase tracking-[0.06em] text-ink">
                  <span className={cn('h-2.5 w-2.5 rounded-full', GROUP_DOT[group.key])} aria-hidden />
                  {group.label}
                  {planned > 0 && (
                    <span className="font-normal normal-case tracking-normal text-ink-muted">
                      {currency.format(planned)} · {Math.round((planned / income) * 100)}% of pay
                    </span>
                  )}
                </span>
              </th>
            </tr>
            {group.lines.map((line) => {
              const status = lineStatus(line);
              const detail = line.notes ?? line.topPayees.join(', ');
              return (
                <tr key={line.categoryId ?? 'uncategorized'} className="border-b border-border-subtle last:border-b-0">
                  <td className="px-5 py-3">
                    <p className="font-medium text-ink">{line.name}</p>
                    {detail && <p className="text-xs text-ink-muted">{detail}</p>}
                  </td>
                  <td className="numeric px-3 py-3 text-right text-ink-muted">{currency.format(line.actualMonthly)}</td>
                  <td className="numeric px-3 py-3 text-right font-semibold text-ink">
                    {line.planMonthly > 0 ? currency.format(line.planMonthly) : '—'}
                  </td>
                  <td className="hidden px-3 py-3 md:table-cell">
                    {line.planMonthly > 0 && (
                      <Progress
                        value={Math.min((Math.max(line.actualMonthly, 0) / line.planMonthly) * 100, 100)}
                        height={8}
                        barColor={TONE_BAR[status.tone]}
                      />
                    )}
                  </td>
                  <td className={cn('px-5 py-3 text-xs font-semibold', TONE_TEXT[status.tone])}>{status.label}</td>
                </tr>
              );
            })}
          </tbody>
        );
      })}
    </table>
    <p className="border-t border-border px-5 py-4 text-xs text-ink-muted">
      The plan spends {currency.format(plan.mustPay + plan.flexible)} and saves {currency.format(plan.saving)} of a
      normal {currency.format(income)} month.{' '}
      {months === 1
        ? `This month has spent ${currency.format(actual.mustPay + actual.flexible)} so far, leaving ${currency.format(actual.saving)}.`
        : `Over the last ${months} months a month spent ${currency.format(actual.mustPay + actual.flexible)} on average, leaving ${currency.format(actual.saving)}.`}
    </p>
  </Card>
);

export default BudgetTable;
