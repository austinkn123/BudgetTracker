import { Landmark } from 'lucide-react';
import { Alert, Button, Card } from '../../../shared/components/ui';
import { useLinkedAccounts } from '../hooks/useLinkedAccounts';
import PlaidLinkLauncher from './PlaidLinkLauncher';

/**
 * Shown on Home and Transactions while no bank is linked, so a new person sees the first step where they land
 * instead of empty charts. Opens Plaid in place; Settings keeps the full list once a bank is linked.
 */
const ConnectBankCard = () => {
  const { linkToken, isPreparingLink, isExchanging, errorMessage, prepareLink, clearLinkToken, handleLinkSuccess, clearError } =
    useLinkedAccounts();

  return (
    <Card>
      <div className="flex flex-col items-start gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-start gap-3">
          <span className="mt-0.5 shrink-0 rounded-full bg-primary-subtle p-2 text-primary" aria-hidden>
            <Landmark size={18} />
          </span>
          <div>
            <p className="text-sm font-semibold text-ink">Connect your bank to get started</p>
            <p className="mt-1 text-sm text-ink-muted">
              Your transactions import on their own, and your budget fills in from what you really spend.
            </p>
          </div>
        </div>
        <Button onClick={prepareLink} loading={isPreparingLink || isExchanging} disabled={linkToken !== null}>
          {isPreparingLink ? 'Preparing…' : isExchanging ? 'Linking…' : 'Connect a bank'}
        </Button>
      </div>
      {errorMessage && <Alert severity="error" message={errorMessage} onClose={clearError} className="mt-4" />}
      {linkToken && <PlaidLinkLauncher linkToken={linkToken} onSuccess={handleLinkSuccess} onExit={clearLinkToken} />}
    </Card>
  );
};

export default ConnectBankCard;
