import { Alert, Badge, Button, Card, Spinner } from '../../../shared/components/ui';
import type { BadgeColor } from '../../../shared/components/ui';
import type { WebullState } from '../../../shared/types/api';
import { errorMessage } from '../../../shared/utils/errorMessage';
import { localTime } from '../../../shared/utils/format';
import { useApprovalPolling, useVerifyWebull, useWebull } from '../hooks/useWebull';

const STATE_BADGE: Record<WebullState, { label: string; color: BadgeColor }> = {
  NotSetUp: { label: 'Not set up', color: 'neutral' },
  NotConnected: { label: 'Not connected', color: 'neutral' },
  WaitingForApproval: { label: 'Waiting for approval', color: 'info' },
  Connected: { label: 'Connected', color: 'success' },
  ApprovalExpired: { label: 'Approval expired', color: 'warning' },
  NeedsVerification: { label: 'Needs verification', color: 'warning' },
};

const Code = ({ children }: { children: string }) => (
  <code className="rounded-sm bg-border-subtle px-1 py-0.5 text-xs text-ink">{children}</code>
);

/**
 * Settings section for the read-only Webull connection. It never accepts or shows a key, secret
 * or token, and has no disconnect: the keys live in the server's config file. The only action is
 * asking Webull to text an approval code, offered only when the server says it can.
 */
const WebullSection = () => {
  const { data: view, isPending, isError } = useWebull();
  const verify = useVerifyWebull();
  useApprovalPolling(view);

  const badge = view && STATE_BADGE[view.state];

  return (
    <Card
      title="Webull"
      subtitle="Your brokerage balance, read-only. It never feeds your budget."
      actions={
        <>
          {badge && <Badge label={badge.label} color={badge.color} />}
          {view?.canVerify && (
            // `loading` disables it while pending: every click texts a new SMS code.
            <Button size="sm" onClick={() => verify.mutate()} loading={verify.isPending}>
              Verify in Webull app
            </Button>
          )}
        </>
      }
      contentClassName="flex flex-col gap-3 text-sm text-ink-muted"
    >
      {verify.isError && (
        <Alert
          severity="error"
          message={errorMessage(verify.error, "Couldn't start verification. Please try again.")}
          onClose={verify.reset}
        />
      )}

      {view?.lastError === 'KeyRejected' && (
        <Alert
          severity="error"
          message="Webull rejected the app key or secret. Check Webull:AppKey and Webull:AppSecret in appsettings.Local.json."
        />
      )}

      {isPending ? (
        <div className="flex items-center gap-2">
          <Spinner size={16} />
          <span>Checking Webull…</span>
        </div>
      ) : isError && !view ? (
        <p>We couldn't check your Webull connection. Reload the page to try again.</p>
      ) : view?.state === 'NotSetUp' ? (
        <p>
          <Code>Webull:AppKey</Code> and <Code>Webull:AppSecret</Code> go in{' '}
          <Code>BudgetTracker.Server/appsettings.Local.json</Code>. This page never asks for
          them, so they never pass through the browser.
        </p>
      ) : view?.state === 'WaitingForApproval' ? (
        <p>
          Approve this in the Webull app with the SMS code.
          {view.approvalExpiresAt && ` Expires at ${localTime(view.approvalExpiresAt)}.`}
        </p>
      ) : (
        <p>Balances show on your Dashboard.</p>
      )}
    </Card>
  );
};

export default WebullSection;
