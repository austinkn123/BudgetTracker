import { useState } from 'react';

import { Alert, Button, Card, ConfirmModal, Spinner } from '../../../shared/components/ui';
import type { PlaidConnectionView } from '../../../shared/types/api';
import { useLinkedAccounts } from '../hooks/useLinkedAccounts';
import LinkedAccountCard from './LinkedAccountCard';
import PlaidLinkLauncher from './PlaidLinkLauncher';

/**
 * Settings-page section that owns bank-link UX across every institution:
 * - "Add institution" is always available; linking never replaces an existing connection
 * - One LinkedAccountCard per institution, each with its own confirmed Disconnect
 * - "Refresh all" re-pulls transactions from every institution
 */
const LinkedAccountsSection = () => {
  const {
    connections,
    isLoadingConnections,
    linkToken,
    isPreparingLink,
    isExchanging,
    isSyncing,
    disconnectingItemId,
    errorMessage,
    lastSync,
    prepareLink,
    clearLinkToken,
    exchangePublicToken,
    refresh,
    disconnect,
    clearError,
  } = useLinkedAccounts();

  const [pendingDisconnect, setPendingDisconnect] = useState<PlaidConnectionView | null>(null);

  const isLinking = isPreparingLink || isExchanging || linkToken !== null;
  const isBusy = isLinking || isSyncing || disconnectingItemId !== null;

  const handleConfirmDisconnect = async () => {
    if (!pendingDisconnect) return;
    await disconnect(pendingDisconnect.plaidItemId);
    // Close on failure too: the error Alert below the list explains what happened.
    setPendingDisconnect(null);
  };

  const addLabel = isPreparingLink ? 'Preparing…' : isExchanging ? 'Linking…' : 'Add institution';
  const hasConnections = connections.length > 0;

  return (
    <Card
      title="Linked banks"
      subtitle="Transactions from every linked institution import automatically."
      actions={
        hasConnections && (
          <>
            <Button variant="secondary" onClick={refresh} loading={isSyncing} disabled={isBusy}>
              {isSyncing ? 'Refreshing…' : 'Refresh all'}
            </Button>
            <Button onClick={prepareLink} loading={isPreparingLink || isExchanging} disabled={isBusy}>
              {addLabel}
            </Button>
          </>
        )
      }
      contentClassName="flex flex-col gap-3"
    >
      {/*
        Errors sit above the list, next to the action that caused them. The server's message is
        shown verbatim because it is written for the person (e.g. "This institution is already
        linked. Disconnect it first if you want to link it again.").
      */}
      {errorMessage && (
        <Alert severity="error" message={errorMessage} onClose={clearError} />
      )}

      {isLoadingConnections ? (
        <div className="flex items-center gap-2 text-ink-muted">
          <Spinner size={16} />
          <span className="text-sm">Checking your connections…</span>
        </div>
      ) : hasConnections ? (
        <ul className="flex flex-col gap-3" aria-label="Linked institutions">
          {connections.map((connection) => (
            <li key={connection.plaidItemId}>
              <LinkedAccountCard
                connection={connection}
                isDisconnecting={disconnectingItemId === connection.plaidItemId}
                disabled={isBusy}
                onDisconnect={setPendingDisconnect}
              />
            </li>
          ))}
        </ul>
      ) : (
        <div className="space-y-2">
          <p className="text-sm text-ink-muted">
            Link your first bank so its transactions import automatically. You can add more
            institutions afterwards.
          </p>
          <Button onClick={prepareLink} loading={isPreparingLink || isExchanging} disabled={isBusy}>
            {isPreparingLink ? 'Preparing…' : isExchanging ? 'Linking…' : 'Connect a bank'}
          </Button>
        </div>
      )}

      {lastSync && (
        <span className="block text-xs text-ink-muted">
          Last sync added {lastSync.inserted}, updated {lastSync.updated}, removed {lastSync.removed}.
        </span>
      )}

      {linkToken && (
        <PlaidLinkLauncher
          linkToken={linkToken}
          onSuccess={exchangePublicToken}
          onExit={clearLinkToken}
        />
      )}

      <ConfirmModal
        open={pendingDisconnect !== null}
        title={`Disconnect ${pendingDisconnect?.institutionName ?? 'this bank'}?`}
        message={
          <>
            Imported transactions stay in your history, but no new transactions will sync from{' '}
            <strong>{pendingDisconnect?.institutionName ?? 'it'}</strong> afterwards. Your other
            linked banks aren't affected.
          </>
        }
        confirmLabel="Disconnect"
        destructive
        isPending={disconnectingItemId !== null}
        onCancel={() => setPendingDisconnect(null)}
        onConfirm={handleConfirmDisconnect}
      />
    </Card>
  );
};

export default LinkedAccountsSection;
