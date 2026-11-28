import { useState } from 'react';

import { Alert, Button, Card, ErrorState, Modal, ModalActions, Spinner } from '../../../shared/components/ui';
import type { PlaidConnectionView } from '../../../shared/types/api';
import { cn } from '../../../shared/utils/cn';
import { useLinkedAccounts } from '../hooks/useLinkedAccounts';
import LinkedAccountCard from './LinkedAccountCard';
import PlaidLinkLauncher from './PlaidLinkLauncher';
import { usePlaidConnections } from '../hooks/usePlaidConnections';

/**
 * Settings-page section that owns bank-link UX across every institution:
 * - "Add institution" is always available; linking never replaces an existing connection
 * - One LinkedAccountCard per institution, each with its own confirmed Disconnect that asks whether to keep or
 *   delete the transactions it imported (BUD-10)
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
    reconnectingItemId,
    disconnectingItemId,
    errorMessage,
    lastSync,
    prepareLink,
    prepareReconnect,
    clearLinkToken,
    handleLinkSuccess,
    refresh,
    disconnect,
    clearError,
  } = useLinkedAccounts();

  // Same cache entry the hook reads: here only to tell a failed read from an empty one.
  const connectionsQuery = usePlaidConnections();
  const [pendingDisconnect, setPendingDisconnect] = useState<PlaidConnectionView | null>(null);
  // Keep is the default: it is the reversible choice, since a bank can be linked again.
  const [deleteTransactions, setDeleteTransactions] = useState(false);

  const openDisconnect = (connection: PlaidConnectionView) => {
    setDeleteTransactions(false);
    setPendingDisconnect(connection);
  };

  const isLinking =
    isPreparingLink || isExchanging || linkToken !== null || reconnectingItemId !== null;
  const isBusy = isLinking || isSyncing || disconnectingItemId !== null;

  const handleConfirmDisconnect = async () => {
    if (!pendingDisconnect) return;
    await disconnect(pendingDisconnect.plaidItemId, deleteTransactions);
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
      ) : connectionsQuery.isError && !hasConnections ? (
        // A failed read is not "no banks": offering to link a first bank here would be wrong.
        <ErrorState
          layout="inline"
          title="Couldn't load your linked banks"
          onRetry={() => void connectionsQuery.refetch()}
          isRetrying={connectionsQuery.isFetching}
        />
      ) : hasConnections ? (
        <ul className="flex flex-col gap-3" aria-label="Linked institutions">
          {connections.map((connection) => (
            <li key={connection.plaidItemId}>
              <LinkedAccountCard
                connection={connection}
                isReconnecting={reconnectingItemId === connection.plaidItemId}
                isDisconnecting={disconnectingItemId === connection.plaidItemId}
                disabled={isBusy}
                onReconnect={prepareReconnect}
                onDisconnect={openDisconnect}
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
          onSuccess={handleLinkSuccess}
          onExit={clearLinkToken}
        />
      )}

      <Modal
        open={pendingDisconnect !== null}
        onClose={() => setPendingDisconnect(null)}
        title={`Disconnect ${pendingDisconnect?.institutionName ?? 'this bank'}?`}
        description={
          <>
            BudgetTracker's access to <strong>{pendingDisconnect?.institutionName ?? 'it'}</strong> is
            removed at Plaid and your stored bank credentials are deleted. Your other linked banks
            aren't affected.
          </>
        }
        maxWidth="xs"
        disableBackdropClose={disconnectingItemId !== null}
        actions={
          <ModalActions
            onCancel={() => setPendingDisconnect(null)}
            onConfirm={handleConfirmDisconnect}
            confirmLabel={deleteTransactions ? 'Disconnect and delete' : 'Disconnect'}
            confirmVariant="destructive"
            isPending={disconnectingItemId !== null}
          />
        }
      >
        <fieldset className="flex flex-col gap-2">
          <legend className="mb-1 text-sm font-medium text-ink">Transactions it imported</legend>
          <DisconnectChoice
            checked={!deleteTransactions}
            onSelect={() => setDeleteTransactions(false)}
            label="Keep imported transactions"
            hint="They stay in your history; nothing new syncs."
          />
          <DisconnectChoice
            checked={deleteTransactions}
            onSelect={() => setDeleteTransactions(true)}
            label={`Delete all imported transactions from ${pendingDisconnect?.institutionName ?? 'this bank'}`}
            hint="Manual transactions are kept. This can't be undone."
            tone="danger"
          />
        </fieldset>
      </Modal>
    </Card>
  );
};

type DisconnectChoiceProps = {
  checked: boolean;
  onSelect: () => void;
  label: string;
  hint: string;
  /** `danger` marks a destructive option: red rather than brand purple when selected. */
  tone?: 'danger';
};

/** One option of the disconnect dialog's keep / delete choice. */
const DisconnectChoice = ({ checked, onSelect, label, hint, tone }: DisconnectChoiceProps) => (
  <label
    className={cn(
      'flex cursor-pointer items-start gap-3 rounded-md border px-3 py-2.5 transition-colors duration-120',
      // A destructive choice reads red when picked, matching the red confirm button.
      !checked && 'border-border hover:border-border-strong',
      checked && (tone === 'danger' ? 'border-error bg-error-subtle' : 'border-primary bg-primary-subtle'),
    )}
  >
    {/* Native <input>: radio option; there is no shared radio primitive, and a native group keeps arrow-key navigation. */}
    <input
      type="radio"
      name="disconnect-transactions"
      checked={checked}
      onChange={onSelect}
      className={cn('focus-ring mt-0.5', tone === 'danger' ? 'accent-error' : 'accent-primary')}
    />
    <span className="flex flex-col">
      <span className="text-sm font-medium text-ink">{label}</span>
      <span className="text-xs text-ink-muted">{hint}</span>
    </span>
  </label>
);

export default LinkedAccountsSection;
