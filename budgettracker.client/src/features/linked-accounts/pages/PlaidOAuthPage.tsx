import { useState } from 'react';
import { Link, Navigate } from 'react-router-dom';

import { Alert, Spinner } from '../../../shared/components/ui';
import PlaidLinkLauncher from '../components/PlaidLinkLauncher';
import { loadPlaidLink, useLinkedAccounts } from '../hooks/useLinkedAccounts';

/**
 * Where a bank's OAuth flow returns when it couldn't open in a popup (phones, in-app browsers).
 * Resumes the Link session saved before the redirect, finishes it the same way the Settings
 * section does (exchange, or mark reconnected and sync), then goes back to Settings.
 */
const PlaidOAuthPage = () => {
  const [saved] = useState(loadPlaidLink);
  const { linkToken, errorMessage, handleLinkSuccess, clearLinkToken } = useLinkedAccounts(saved);
  const [isDone, setIsDone] = useState(false);

  if (!saved) return <Navigate to="/settings" replace />;

  if (errorMessage) {
    return (
      <Alert severity="error" message={errorMessage}>
        <Link
          to="/settings"
          className="focus-ring mt-1 block w-fit rounded-sm font-semibold underline underline-offset-2"
        >
          Back to Settings
        </Link>
      </Alert>
    );
  }

  if (isDone) return <Navigate to="/settings" replace />;

  return (
    <div className="flex items-center gap-2 text-ink-muted" role="status">
      <Spinner size={16} />
      <span className="text-sm">Finishing your bank connection…</span>
      {linkToken && (
        <PlaidLinkLauncher
          linkToken={linkToken}
          receivedRedirectUri={window.location.href}
          onSuccess={async (publicToken) => {
            await handleLinkSuccess(publicToken);
            setIsDone(true);
          }}
          onExit={() => {
            clearLinkToken();
            setIsDone(true);
          }}
        />
      )}
    </div>
  );
};

export default PlaidOAuthPage;
