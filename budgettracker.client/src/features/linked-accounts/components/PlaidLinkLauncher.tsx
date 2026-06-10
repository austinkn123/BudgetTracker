import { useEffect } from 'react';
import { usePlaidLink } from 'react-plaid-link';

interface PlaidLinkLauncherProps {
  /** Token returned by /api/plaid/link-token. When set, the Plaid Link UI auto-opens. */
  linkToken: string;
  onSuccess: (publicToken: string) => void;
  onExit: () => void;
}

/**
 * Headless adapter around react-plaid-link's usePlaidLink. Auto-opens the Plaid Link UI
 * as soon as <see cref="linkToken"/> becomes available and forwards the resulting
 * public_token (or exit) back to the parent.
 */
export const PlaidLinkLauncher = ({ linkToken, onSuccess, onExit }: PlaidLinkLauncherProps) => {
  const { open, ready } = usePlaidLink({
    token: linkToken,
    onSuccess: (publicToken) => onSuccess(publicToken),
    onExit: () => onExit(),
  });

  useEffect(() => {
    if (ready) {
      open();
    }
  }, [ready, open]);

  return null;
};
