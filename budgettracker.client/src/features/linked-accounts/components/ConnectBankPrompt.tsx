import { Landmark } from 'lucide-react';
import { Alert, Button, EmptyState } from '../../../shared/components/ui';
import { useLinkedAccounts } from '../hooks/useLinkedAccounts';
import { usePlaidConnections } from '../hooks/usePlaidConnections';
import PlaidLinkLauncher from './PlaidLinkLauncher';

type ConnectBankPromptProps = {
  /** What a linked bank fills in on this page, as a full sentence. */
  description: string;
};

/**
 * The first step for someone with no bank linked, on the pages a bank fills in. It opens Plaid
 * Link right here rather than sending them to Settings, and drops away once a bank is linked.
 *
 * Shown only once the connections read has succeeded with none: while it loads, or if it fails,
 * nobody knows whether a bank is linked, and saying "no bank" then would be wrong.
 */
const ConnectBankPrompt = ({ description }: ConnectBankPromptProps) => {
  const { data: connections, isSuccess } = usePlaidConnections();
  if (!isSuccess || connections.length > 0) return null;
  return <ConnectBank description={description} />;
};

/** Split out so the link mutations only exist while the prompt is on screen. */
const ConnectBank = ({ description }: ConnectBankPromptProps) => {
  const {
    linkToken,
    isPreparingLink,
    isExchanging,
    errorMessage,
    prepareLink,
    clearLinkToken,
    handleLinkSuccess,
    clearError,
  } = useLinkedAccounts();

  return (
    <div className="space-y-3">
      <EmptyState
        icon={<Landmark size={18} />}
        title="No bank linked yet"
        description={description}
        action={
          <Button
            onClick={prepareLink}
            loading={isPreparingLink || isExchanging}
            disabled={linkToken !== null}
          >
            {isPreparingLink ? 'Preparing…' : isExchanging ? 'Linking…' : 'Connect a bank'}
          </Button>
        }
      />

      {errorMessage && <Alert severity="error" message={errorMessage} onClose={clearError} />}

      {linkToken && (
        <PlaidLinkLauncher linkToken={linkToken} onSuccess={handleLinkSuccess} onExit={clearLinkToken} />
      )}
    </div>
  );
};

export default ConnectBankPrompt;
