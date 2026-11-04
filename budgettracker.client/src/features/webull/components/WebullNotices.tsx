import { Link } from 'react-router-dom';
import { Alert } from '../../../shared/components/ui';
import type { WebullErrorKind, WebullState } from '../../../shared/types/api';
import { localDateTime } from '../../../shared/utils/format';

/**
 * The Webull status messaging shared by the Dashboard card and the Investments page, so the two
 * never word the same situation differently.
 */

const settingsLinkClass = 'focus-ring rounded-sm font-semibold underline underline-offset-2';

interface WebullAsOfProps {
  asOf: string | null;
  isStale: boolean;
  /** Leads the time when fresh, e.g. "As of" or "Prices from Webull as of". */
  label: string;
}

/** When the figures are from, or a warning once the server calls them stale. Nothing without a time. */
export const WebullAsOf = ({ asOf, isStale, label }: WebullAsOfProps) => {
  if (!asOf) return null;
  return isStale ? (
    <span className="font-medium text-warning-dark">Not updated since {localDateTime(asOf)}</span>
  ) : (
    `${label} ${localDateTime(asOf)}`
  );
};

/** What can't update, in the copy: "balance" by default, "trade history" on the Trades tab. */
interface SubjectProps {
  subject?: string;
}

interface WebullAlertsProps extends SubjectProps {
  state: WebullState;
  lastError: WebullErrorKind | null;
}

/** What stops the figures updating, each with the way out. Shown alongside, never instead of, the data. */
export const WebullAlerts = ({ state, lastError, subject = 'balance' }: WebullAlertsProps) => (
  <>
    {state !== 'Connected' && (
      <Alert
        severity="warning"
        title={`Webull needs you to verify in the Webull app before the ${subject} can update`}
      >
        <Link to="/settings" className={settingsLinkClass}>
          Verify in Settings
        </Link>
      </Alert>
    )}

    {lastError === 'KeyRejected' && (
      <Alert severity="error" title="Webull rejected the app key or secret">
        The {subject} can't update until the keys are fixed.{' '}
        <Link to="/settings" className={settingsLinkClass}>
          See Settings
        </Link>
      </Alert>
    )}
  </>
);

interface WebullDataNoteProps extends SubjectProps {
  lastError: WebullErrorKind | null;
  hasData: boolean;
}

/** Under the figures: why they may be old. Without figures: that there are none, never a $0. */
export const WebullDataNote = ({ lastError, hasData, subject = 'balance' }: WebullDataNoteProps) => {
  if (!hasData) return <p className="text-sm text-ink-muted">Webull {subject} unavailable right now</p>;
  if (lastError !== 'Unreachable') return null;
  return (
    <p className="text-xs text-ink-muted">Couldn't reach Webull. Showing the last known {subject}.</p>
  );
};
