import { useState } from 'react';

import { Alert, Badge, Button, Card, Spinner } from '../../../shared/components/ui';
import { useLinkedAccount } from '../hooks/useLinkedAccount';
import PlaidLinkLauncher from './PlaidLinkLauncher';
import ReplaceConnectionDialog from './ReplaceConnectionDialog';

/**
 * Settings-page card that owns the bank-link UX:
 *   - "Connect a bank" when no connection exists (AC-1: enabled on mount, token fetched on click)
 *   - Connected state with Refresh + Disconnect when a PlaidItem is active (AC-2)
 *   - Replace-confirmation dialog when user clicks Connect while already linked (AC-10)
 */
const LinkedAccountCard = () => {
  const {
    connection,
    isLoadingConnection,
    linkToken,
    isPreparingLink,
    isExchanging,
    isSyncing,
    isDisconnecting,
    errorMessage,
    lastSync,
    prepareLink,
    clearLinkToken,
    exchangePublicToken,
    refresh,
    disconnect,
  } = useLinkedAccount();

  const [replaceDialogOpen, setReplaceDialogOpen] = useState(false);

  const handleConnectClick = async () => {
    if (connection) {
      setReplaceDialogOpen(true);
      return;
    }
    await prepareLink();
  };

  const handleConfirmReplace = async () => {
    setReplaceDialogOpen(false);
    await disconnect();
    await prepareLink();
  };

  const handlePlaidSuccess = async (publicToken: string) => {
    await exchangePublicToken(publicToken);
  };

  const handlePlaidExit = () => {
    clearLinkToken();
  };

  const formattedLastSync = (() => {
    const ts = lastSync?.syncedAt ?? connection?.lastSyncedAt;
    if (!ts) return 'never';
    return new Date(ts).toLocaleString();
  })();

  return (
    <Card title="Linked Bank" contentClassName="flex flex-col gap-3">
      {isLoadingConnection ? (
        <div className="flex items-center gap-2 text-ink-muted">
          <Spinner size={16} />
          <span className="text-sm">Checking connection…</span>
        </div>
      ) : connection ? (
        <div className="space-y-3">
          <div className="flex items-center gap-2 flex-wrap">
            <p className="text-body font-medium text-ink">
              Connected to {connection.institutionName}
            </p>
            <Badge label="Active" color="success" />
          </div>
          {connection.accounts.length > 0 && (
            <div className="flex gap-2 flex-wrap">
              {connection.accounts.map((account) => (
                <Badge
                  key={`${account.name}-${account.mask ?? ''}`}
                  label={`${account.name}${account.mask ? ` ••${account.mask}` : ''}`}
                  variant="outline"
                />
              ))}
            </div>
          )}
          <span className="block text-xs text-ink-muted">
            Last synced: {formattedLastSync}
          </span>
          <div className="flex gap-2 flex-wrap">
            <Button onClick={refresh} loading={isSyncing} disabled={isDisconnecting}>
              {isSyncing ? 'Refreshing…' : 'Refresh'}
            </Button>
            <Button
              variant="secondary"
              onClick={handleConnectClick}
              disabled={isPreparingLink || isSyncing || isDisconnecting || isExchanging}
            >
              {isPreparingLink ? 'Preparing…' : 'Connect a different bank'}
            </Button>
            <Button
              variant="destructive"
              onClick={disconnect}
              loading={isDisconnecting}
              disabled={isSyncing}
            >
              {isDisconnecting ? 'Disconnecting…' : 'Disconnect'}
            </Button>
          </div>
        </div>
      ) : (
        <div className="space-y-2">
          <p className="text-sm text-ink-muted">
            Link your bank so transactions import automatically.
          </p>
          <Button
            onClick={handleConnectClick}
            loading={isPreparingLink || isExchanging}
          >
            {isPreparingLink ? 'Preparing…' : isExchanging ? 'Linking…' : 'Connect a bank'}
          </Button>
        </div>
      )}

      {lastSync && (
        <span className="block text-xs text-ink-muted">
          Last sync added {lastSync.inserted}, updated {lastSync.updated}, removed {lastSync.removed}.
        </span>
      )}

      {errorMessage && <Alert severity="error" message={errorMessage} />}

      {linkToken && (
        <PlaidLinkLauncher
          linkToken={linkToken}
          onSuccess={handlePlaidSuccess}
          onExit={handlePlaidExit}
        />
      )}

      <ReplaceConnectionDialog
        open={replaceDialogOpen}
        currentInstitutionName={connection?.institutionName}
        onCancel={() => setReplaceDialogOpen(false)}
        onConfirm={handleConfirmReplace}
      />
    </Card>
  );
};

export default LinkedAccountCard;
