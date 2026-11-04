import { Link } from 'react-router-dom';
import { Button, Card, Skeleton } from '../../../shared/components/ui';
import { inCurrency } from '../../../shared/utils/format';
import { useWebull } from '../hooks/useWebull';
import { WebullAlerts, WebullAsOf, WebullDataNote } from './WebullNotices';

const figureLabelClass = 'text-2xs font-semibold uppercase tracking-[0.08em] text-ink-muted';

/**
 * Read-only Webull balances on the Dashboard. Deliberately separate from every budget figure:
 * one row per account in that account's own currency, and no combined total.
 *
 * Owns its own query so a slow or failing Webull never feeds the page's loading/error state.
 * It never shows $0 as a stand-in: before data there is a placeholder, and without a balance it
 * says so.
 */
const WebullBalanceCard = () => {
  const { data: view, isPending } = useWebull();

  if (!view) {
    // No data and the request failed: render nothing rather than a card that can only apologise.
    if (!isPending) return null;
    return (
      <Card title="Webull">
        <div role="status" className="space-y-2">
          <span className="sr-only">Loading your Webull balance</span>
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-7 w-48" />
        </div>
      </Card>
    );
  }

  if (view.state === 'NotSetUp') return null;

  const hasBalances = view.balances.length > 0;

  return (
    <Card
      title="Webull"
      // Guarded here too: Card renders a subtitle line for any element, even one that renders nothing.
      subtitle={view.asOf && <WebullAsOf asOf={view.asOf} isStale={view.isStale} label="As of" />}
      actions={
        <Button component={Link} to="/investments" variant="secondary" size="sm">
          View holdings
        </Button>
      }
      contentClassName="flex flex-col gap-4"
    >
      <WebullAlerts state={view.state} lastError={view.lastError} />

      {hasBalances && (
        <ul aria-label="Webull accounts" className="divide-y divide-border-subtle">
          {view.balances.map((balance) => (
            <li
              key={balance.accountId}
              className="flex flex-wrap items-end justify-between gap-x-8 gap-y-2 py-3 first:pt-0 last:pb-0"
            >
              <p className="text-sm font-semibold text-ink">
                {balance.accountLabel ?? balance.accountType}
              </p>
              <dl className="flex gap-8">
                <div>
                  <dt className={figureLabelClass}>Total value</dt>
                  <dd className="numeric mt-1 text-xl font-semibold tracking-[-0.02em] text-ink">
                    {inCurrency(balance.totalNetLiquidationValue, balance.currency)}
                  </dd>
                </div>
                <div>
                  <dt className={figureLabelClass}>Cash</dt>
                  <dd className="numeric mt-1 text-xl font-semibold tracking-[-0.02em] text-ink-muted">
                    {balance.totalCashBalance === null
                      ? '—'
                      : inCurrency(balance.totalCashBalance, balance.currency)}
                  </dd>
                </div>
              </dl>
            </li>
          ))}
        </ul>
      )}

      <WebullDataNote lastError={view.lastError} hasData={hasBalances} />
    </Card>
  );
};

export default WebullBalanceCard;
