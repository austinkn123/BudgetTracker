import { useState } from 'react';
import Typography from '@mui/material/Typography';
import CircularProgress from '@mui/material/CircularProgress';
import { BudAlert, BudBadge, BudButton, BudCard } from '../../../shared/components/ui';
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
    <BudCard title="Linked Bank" contentSx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
      {isLoadingConnection ? (
        <div className="flex items-center gap-2 text-ink-muted">
          <CircularProgress size={16} />
          <Typography variant="body2">Checking connection…</Typography>
        </div>
      ) : connection ? (
        <div className="space-y-3">
          <div className="flex items-center gap-2 flex-wrap">
            <Typography variant="body1" className="font-medium">
              Connected to {connection.institutionName}
            </Typography>
            <BudBadge label="Active" color="success" />
          </div>
          {connection.accounts.length > 0 && (
            <div className="flex gap-2 flex-wrap">
              {connection.accounts.map((account) => (
                <BudBadge
                  key={`${account.name}-${account.mask ?? ''}`}
                  label={`${account.name}${account.mask ? ` ••${account.mask}` : ''}`}
                  variant="outline"
                />
              ))}
            </div>
          )}
          <Typography variant="caption" className="text-ink-muted block">
            Last synced: {formattedLastSync}
          </Typography>
          <div className="flex gap-2 flex-wrap">
            <BudButton onClick={refresh} loading={isSyncing} disabled={isDisconnecting}>
              {isSyncing ? 'Refreshing…' : 'Refresh'}
            </BudButton>
            <BudButton
              variant="secondary"
              onClick={handleConnectClick}
              disabled={isPreparingLink || isSyncing || isDisconnecting || isExchanging}
            >
              {isPreparingLink ? 'Preparing…' : 'Connect a different bank'}
            </BudButton>
            <BudButton
              variant="destructive"
              onClick={disconnect}
              loading={isDisconnecting}
              disabled={isSyncing}
            >
              {isDisconnecting ? 'Disconnecting…' : 'Disconnect'}
            </BudButton>
          </div>
        </div>
      ) : (
        <div className="space-y-2">
          <Typography variant="body2" className="text-ink-muted">
            Link your bank so transactions import automatically.
          </Typography>
          <BudButton
            onClick={handleConnectClick}
            loading={isPreparingLink || isExchanging}
          >
            {isPreparingLink ? 'Preparing…' : isExchanging ? 'Linking…' : 'Connect a bank'}
          </BudButton>
        </div>
      )}

      {lastSync && (
        <Typography variant="caption" className="text-ink-muted block">
          Last sync added {lastSync.inserted}, updated {lastSync.updated}, removed {lastSync.removed}.
        </Typography>
      )}

      {errorMessage && <BudAlert severity="error" message={errorMessage} />}

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
    </BudCard>
  );
};

export default LinkedAccountCard;
