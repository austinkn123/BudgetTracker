import { useEffect, useState, type ReactNode } from 'react';
import { Hub } from 'aws-amplify/utils';
import {
  getCurrentUser,
  fetchAuthSession,
  signIn,
  resetPassword,
  confirmResetPassword,
  signOut,
} from 'aws-amplify/auth';
import { AuthContext, type AuthContextType, type AuthUser } from './authContextValue';

type AuthProviderProps = {
  children: ReactNode;
};

export const AuthProvider = ({ children }: AuthProviderProps) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [idToken, setIdToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

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

  const handleSignIn = async (email: string, password: string) => {
    try {
      const output = await signIn({
        username: email,
        password,
      });

      if (output.isSignedIn) {
        const session = await fetchAuthSession();
        const token = session.tokens?.idToken?.toString();
        const currentUser = await getCurrentUser();

        setUser({
          username: currentUser.username,
          email: currentUser.signInDetails?.loginId || email,
        });
        setIdToken(token || null);
      }
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
    forgotPassword: handleForgotPassword,
    confirmForgotPassword: handleConfirmForgotPassword,
    signOut: handleSignOut,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
