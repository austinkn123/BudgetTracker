// [SPIKE] BUD-4 — throwaway. Do not merge to main without replacing SpikePlaidAccessor.

import Button from '@mui/material/Button';
import { usePlaidLink } from '../hooks/usePlaidLink';

/**
 * Thin MUI button that launches the Plaid Link iframe.
 * Disabled until the link token has been fetched and react-plaid-link reports ready.
 */
export function PlaidLinkButton() {
  const { open, ready, error, isExchanging } = usePlaidLink();

  return (
    <div className="space-y-2">
      <Button
        variant="contained"
        onClick={() => open()}
        disabled={!ready || isExchanging}
      >
        {isExchanging ? 'Connecting…' : 'Connect a bank account'}
      </Button>
      {error && (
        <p className="text-sm text-red-600">{error}</p>
      )}
    </div>
  );
}
