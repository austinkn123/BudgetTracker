import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Alert } from '../../../shared/components/ui';
import { usePlaidConnections } from '../hooks/usePlaidConnections';

const listFormat = new Intl.ListFormat('en-US', { type: 'conjunction' });

/** A paused bank means every figure on the page may be missing recent activity, so it goes above them. */
const ReconnectAlert = () => {
  // Shared cache with Settings; never blocks the page.
  const { data: connections = [] } = usePlaidConnections();
  const needsSignIn = useMemo(
    () => connections.filter((c) => c.needsReconnect).map((c) => c.institutionName),
    [connections],
  );

  if (needsSignIn.length === 0) return null;

  return (
    <Alert
      severity="warning"
      title={`${listFormat.format(needsSignIn)} ${needsSignIn.length === 1 ? 'needs' : 'need'} you to sign in again`}
    >
      New transactions won't import until you do, so this page may be missing recent activity.{' '}
      <Link to="/settings" className="focus-ring rounded-sm font-semibold underline underline-offset-2">
        Reconnect in Settings
      </Link>
    </Alert>
  );
};

export default ReconnectAlert;
