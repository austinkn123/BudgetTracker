import { createContext } from 'react';

// Lives apart from AuthProvider so AuthContext.tsx exports only components (react-refresh).
export type AuthUser = {
  username: string;
  email: string;
};

/** Where sign-in stands after the password: finished, or waiting on an authenticator code. */
export type SignInResult =
  | { step: 'done' }
  | { step: 'totp' }
  | { step: 'totpSetup'; sharedSecret: string; setupUri: string };

export type AuthContextType = {
  user: AuthUser | null;
  idToken: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  signIn: (email: string, password: string) => Promise<SignInResult>;
  /** Submit the 6-digit authenticator code for the sign-in that signIn started. */
  confirmMfa: (code: string) => Promise<void>;
  forgotPassword: (email: string) => Promise<{ deliveryMedium: string }>;
  confirmForgotPassword: (email: string, code: string, newPassword: string) => Promise<void>;
  signOut: () => Promise<void>;
};

export const AuthContext = createContext<AuthContextType | undefined>(undefined);
