import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../auth/useAuth';

export interface UseSignOutResult {
  signOut: () => Promise<void>;
  isSigningOut: boolean;
  error: string | null;
}

/**
 * Sign-out with redirect, extracted from SettingsPage so the nav and any
 * future caller share one implementation (BUD-14).
 */
export const useSignOut = (): UseSignOutResult => {
  const { signOut: cognitoSignOut } = useAuth();
  const navigate = useNavigate();
  const [isSigningOut, setIsSigningOut] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const signOut = useCallback(async () => {
    setIsSigningOut(true);
    setError(null);
    try {
      await cognitoSignOut();
      navigate('/login');
    } catch (err) {
      setError((err as Error).message || 'Sign out failed. Please try again.');
      // Only reset on failure — on success the redirect unmounts the caller.
      setIsSigningOut(false);
    }
  }, [cognitoSignOut, navigate]);

  return { signOut, isSigningOut, error };
};
