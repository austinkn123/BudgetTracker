import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Hub } from 'aws-amplify/utils';
import {
  getCurrentUser,
  fetchAuthSession,
  signIn,
  confirmSignIn,
  type SignInOutput,
  resetPassword,
  confirmResetPassword,
  signOut,
} from 'aws-amplify/auth';
import { AuthContext, type AuthContextType, type AuthUser, type SignInResult } from './authContextValue';

/** Name shown for this account in the authenticator app. */
const TOTP_ISSUER = 'BudgetTracker';

type AuthProviderProps = {
  children: ReactNode;
};

export const AuthProvider = ({ children }: AuthProviderProps) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [idToken, setIdToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  // The email from the password step, for the account name in the setup link and as a loginId fallback.
  const pendingEmail = useRef('');

  useEffect(() => {
    const initializeAuth = async () => {
      try {
        const currentUser = await getCurrentUser();
        const session = await fetchAuthSession();
        const token = session.tokens?.idToken?.toString();

        setUser({
          username: currentUser.username,
          email: currentUser.signInDetails?.loginId || '',
        });
        setIdToken(token || null);
      } catch {
        setUser(null);
        setIdToken(null);
      } finally {
        setIsLoading(false);
      }
    };

    initializeAuth();
  }, []);

  useEffect(() => {
    const unsubscribe = Hub.listen('auth', ({ payload }) => {
      if (payload.event === 'signedOut') {
        setUser(null);
        setIdToken(null);
      } else if (payload.event === 'signInWithRedirect') {
        // Handle sign-in flow if needed
      }
    });

    return () => unsubscribe();
  }, []);

  const completeSignIn = async () => {
    const session = await fetchAuthSession();
    const token = session.tokens?.idToken?.toString();
    const currentUser = await getCurrentUser();

    setUser({
      username: currentUser.username,
      email: currentUser.signInDetails?.loginId || pendingEmail.current,
    });
    setIdToken(token || null);
  };

  /** Finish the sign-in or say which authenticator step comes next. */
  const resolveStep = async (output: SignInOutput): Promise<SignInResult> => {
    if (output.isSignedIn) {
      await completeSignIn();
      return { step: 'done' };
    }

    const { nextStep } = output;
    switch (nextStep.signInStep) {
      case 'CONTINUE_SIGN_IN_WITH_MFA_SETUP_SELECTION':
        // The app only supports authenticator apps, so answer for the user.
        return resolveStep(await confirmSignIn({ challengeResponse: 'TOTP' }));
      case 'CONFIRM_SIGN_IN_WITH_TOTP_CODE':
        return { step: 'totp' };
      case 'CONTINUE_SIGN_IN_WITH_TOTP_SETUP':
        return {
          step: 'totpSetup',
          sharedSecret: nextStep.totpSetupDetails.sharedSecret,
          setupUri: nextStep.totpSetupDetails.getSetupUri(TOTP_ISSUER, pendingEmail.current).toString(),
        };
      default:
        throw new Error(`Sign-in needs a step this app doesn't support (${nextStep.signInStep}).`);
    }
  };

  const handleSignIn = async (email: string, password: string): Promise<SignInResult> => {
    try {
      pendingEmail.current = email;
      const credentials = { username: email, password };
      const output = await signIn(credentials).catch(async (error: Error) => {
        // A stale session still in Amplify's storage blocks signIn; clear it and retry once.
        if (error.name !== 'UserAlreadyAuthenticatedException') throw error;
        await signOut();
        return signIn(credentials);
      });

      return await resolveStep(output);
    } catch (error) {
      const err = error as Error;
      throw new Error(err.message || 'Sign in failed');
    }
  };

  const handleConfirmMfa = async (code: string) => {
    try {
      const result = await resolveStep(await confirmSignIn({ challengeResponse: code }));
      if (result.step !== 'done') throw new Error('Sign in failed');
    } catch (error) {
      const err = error as Error;
      throw new Error(err.message || 'Sign in failed');
    }
  };

  const handleForgotPassword = async (email: string) => {
    try {
      const output = await resetPassword({
        username: email,
      });
      return {
        deliveryMedium: output.nextStep.codeDeliveryDetails?.deliveryMedium || 'email',
      };
    } catch (error) {
      const err = error as Error;
      if (err.message.includes('not found')) {
        throw new Error('User not found. Please check the email address.');
      }
      throw new Error(err.message || 'Failed to initiate password reset');
    }
  };

  const handleConfirmForgotPassword = async (email: string, code: string, newPassword: string) => {
    try {
      await confirmResetPassword({
        username: email,
        confirmationCode: code,
        newPassword,
      });
    } catch (error) {
      const err = error as Error;
      if (err.message.includes('Attempt limit exceeded')) {
        throw new Error('Too many failed attempts. Please request a new code.');
      }
      if (err.message.includes('Invalid verification code')) {
        throw new Error('Invalid confirmation code. Please try again.');
      }
      throw new Error(err.message || 'Password reset failed');
    }
  };

  const handleSignOut = async () => {
    try {
      await signOut();
      setUser(null);
      setIdToken(null);
    } catch (error) {
      const err = error as Error;
      throw new Error(err.message || 'Sign out failed');
    }
  };

  const value: AuthContextType = {
    user,
    idToken,
    isAuthenticated: !!user && !!idToken,
    isLoading,
    signIn: handleSignIn,
    confirmMfa: handleConfirmMfa,
    forgotPassword: handleForgotPassword,
    confirmForgotPassword: handleConfirmForgotPassword,
    signOut: handleSignOut,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
