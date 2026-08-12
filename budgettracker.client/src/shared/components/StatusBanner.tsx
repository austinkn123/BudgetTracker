import BudAlert from './ui/BudAlert';

type StatusBannerProps = {
  statusMessage: string | null;
  statusError: string | null;
};

/**
 * Success/error banner for feature sections.
 *
 * The props contract is unchanged (BUD-16) so its five callers stay untouched;
 * only the rendering moved onto BudAlert.
 */
const StatusBanner = ({ statusMessage, statusError }: StatusBannerProps) => {
  if (!statusMessage && !statusError) {
    return null;
  }

  return (
    <BudAlert
      severity={statusError ? 'error' : 'success'}
      message={statusError || statusMessage}
    />
  );
};

export default StatusBanner;
