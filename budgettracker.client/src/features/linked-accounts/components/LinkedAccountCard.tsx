import { Badge, Button, Card } from '../../../shared/components/ui';
import type { PlaidConnectionView } from '../../../shared/types/api';

interface LinkedAccountCardProps {
  connection: PlaidConnectionView;
  isDisconnecting: boolean;
  /** Disable the action while another bank operation is in flight. */
  disabled?: boolean;
  onDisconnect: (connection: PlaidConnectionView) => void;
}

const formatLastSynced = (ts: string | null | undefined) =>
  ts ? new Date(ts).toLocaleString() : 'never';

/** One linked institution: its accounts, when it last synced, and its own Disconnect. */
const LinkedAccountCard = ({
  connection,
  isDisconnecting,
  disabled = false,
  onDisconnect,
}: LinkedAccountCardProps) => (
  <Card
    padding="sm"
    title={connection.institutionName}
    subtitle={`Last synced: ${formatLastSynced(connection.lastSyncedAt)}`}
    actions={
      <>
        <Badge label="Active" color="success" />
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

export default LinkedAccountCard;
