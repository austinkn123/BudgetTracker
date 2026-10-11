import { Badge, Button, Card } from '../../../shared/components/ui';
import type { PlaidConnectionView } from '../../../shared/types/api';

interface LinkedAccountCardProps {
  connection: PlaidConnectionView;
  isReconnecting: boolean;
  isDisconnecting: boolean;
  /** Disable the actions while another bank operation is in flight. */
  disabled?: boolean;
  onReconnect: (plaidItemId: number) => void;
  onDisconnect: (connection: PlaidConnectionView) => void;
}

const formatLastSynced = (ts: string | null | undefined) =>
  ts ? new Date(ts).toLocaleString() : 'never';

/**
 * One linked institution: its accounts, when it last synced, and its own Disconnect. When the
 * bank wants the person to sign in again, it says syncing is paused and offers Reconnect.
 */
const LinkedAccountCard = ({
  connection,
  isReconnecting,
  isDisconnecting,
  disabled = false,
  onReconnect,
  onDisconnect,
}: LinkedAccountCardProps) => {
  const lastSynced = `Last synced: ${formatLastSynced(connection.lastSyncedAt)}`;

  return (
    <Card
      padding="sm"
      title={connection.institutionName}
      subtitle={
        connection.needsReconnect
          ? `Syncing is paused until you sign in again. ${lastSynced}.`
          : lastSynced
      }
      actions={
        <>
          {connection.needsReconnect ? (
            <>
              <Badge label="Needs sign-in" color="warning" />
              <Button
                size="sm"
                onClick={() => onReconnect(connection.plaidItemId)}
                loading={isReconnecting}
                disabled={disabled}
                aria-label={`Reconnect ${connection.institutionName}`}
              >
                {isReconnecting ? 'Reconnecting…' : 'Reconnect'}
              </Button>
            </>
          ) : (
            <Badge label="Active" color="success" />
          )}
          <Button
            size="sm"
            variant="destructive"
            onClick={() => onDisconnect(connection)}
            loading={isDisconnecting}
            disabled={disabled}
            aria-label={`Disconnect ${connection.institutionName}`}
          >
            {isDisconnecting ? 'Disconnecting…' : 'Disconnect'}
          </Button>
        </>
      }
    >
      {connection.accounts.length > 0 ? (
        <div className="flex flex-wrap gap-2">
          {connection.accounts.map((account) => (
            <Badge
              key={account.plaidAccountId}
              label={`${account.name}${account.mask ? ` ••${account.mask}` : ''}`}
              variant="outline"
            />
          ))}
        </div>
      ) : (
        <p className="text-sm text-ink-muted">No accounts were shared from this institution.</p>
      )}
    </Card>
  );
};

export default LinkedAccountCard;
