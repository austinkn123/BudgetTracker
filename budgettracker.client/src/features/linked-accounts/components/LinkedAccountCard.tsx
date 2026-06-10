import { useState } from 'react';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import { useLinkedAccount } from '../hooks/useLinkedAccount';
import { PlaidLinkLauncher } from './PlaidLinkLauncher';
import { ReplaceConnectionDialog } from './ReplaceConnectionDialog';

/**
 * Settings-page card that owns the bank-link UX:
 *   - "Connect a bank" when no connection exists (AC-1: enabled on mount, token fetched on click)
 *   - Connected state with Refresh + Disconnect when a PlaidItem is active (AC-2)
 *   - Replace-confirmation dialog when user clicks Connect while already linked (AC-10)
 */
export const LinkedAccountCard = () => {
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
    <div className="border rounded-lg p-4 space-y-3">
      <Typography variant="subtitle1" className="font-semibold text-gray-700">
        Linked Bank
      </Typography>

      {isLoadingConnection ? (
        <div className="flex items-center gap-2 text-gray-500">
          <CircularProgress size={16} />
          <Typography variant="body2">Checking connection…</Typography>
        </div>
      ) : connection ? (
        <div className="space-y-3">
          <div className="flex items-center gap-2 flex-wrap">
            <Typography variant="body1" className="font-medium">
              Connected to {connection.institutionName}
            </Typography>
            <Chip label="Active" color="success" size="small" />
          </div>
          {connection.accounts.length > 0 && (
            <div className="flex gap-2 flex-wrap">
              {connection.accounts.map((account) => (
                <Chip
                  key={`${account.name}-${account.mask ?? ''}`}
                  label={`${account.name}${account.mask ? ` ••${account.mask}` : ''}`}
                  variant="outlined"
                  size="small"
                />
              ))}
            </div>
          )}
          <Typography variant="caption" className="text-gray-500 block">
            Last synced: {formattedLastSync}
          </Typography>
          <div className="flex gap-2 flex-wrap">
            <Button
              variant="contained"
              onClick={refresh}
              disabled={isSyncing || isDisconnecting}
            >
              {isSyncing ? 'Refreshing…' : 'Refresh'}
            </Button>
            <Button
              variant="outlined"
              onClick={handleConnectClick}
              disabled={isPreparingLink || isSyncing || isDisconnecting || isExchanging}
            >
              {isPreparingLink ? 'Preparing…' : 'Connect a different bank'}
            </Button>
            <Button
              variant="outlined"
              color="error"
              onClick={disconnect}
              disabled={isDisconnecting || isSyncing}
            >
              {isDisconnecting ? 'Disconnecting…' : 'Disconnect'}
            </Button>
          </div>
        </div>
      ) : (
        <div className="space-y-2">
          <Typography variant="body2" className="text-gray-600">
            Link your bank so transactions import automatically.
          </Typography>
          <Button
            variant="contained"
            onClick={handleConnectClick}
            disabled={isPreparingLink || isExchanging}
          >
            {isPreparingLink ? 'Preparing…' : isExchanging ? 'Linking…' : 'Connect a bank'}
          </Button>
        </div>
      )}

      {lastSync && (
        <Typography variant="caption" className="text-gray-500 block">
          Last sync added {lastSync.inserted}, updated {lastSync.updated}, removed {lastSync.removed}.
        </Typography>
      )}

      {errorMessage && (
        <Typography variant="body2" className="text-red-600">
          {errorMessage}
        </Typography>
      )}

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
    </div>
  );
};
