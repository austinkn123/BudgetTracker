import type { ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { confirmSignIn, fetchAuthSession, getCurrentUser, signIn, signOut } from 'aws-amplify/auth';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthProvider } from './AuthContext';
import { useAuth } from './useAuth';

vi.mock('aws-amplify/auth', () => ({
  getCurrentUser: vi.fn(),
  fetchAuthSession: vi.fn(),
  signIn: vi.fn(),
  confirmSignIn: vi.fn(),
  signOut: vi.fn(),
  resetPassword: vi.fn(),
  confirmResetPassword: vi.fn(),
}));

vi.mock('aws-amplify/utils', () => ({
  Hub: { listen: () => () => {} },
}));

const amplifyError = (name: string, message: string) =>
  Object.assign(new Error(message), { name });

const renderAuth = async () => {
  const hook = renderHook(() => useAuth(), {
    wrapper: ({ children }: { children: ReactNode }) => <AuthProvider>{children}</AuthProvider>,
  });
  await waitFor(() => expect(hook.result.current.isLoading).toBe(false));
  return hook;
};

describe('AuthProvider signIn', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    // Boots signed out: Amplify has no current user to restore.
    vi.mocked(getCurrentUser).mockRejectedValueOnce(new Error('No current user'));
  });

  it('clears a stale Amplify session and retries when a user is already signed in', async () => {
    vi.mocked(signIn)
      .mockRejectedValueOnce(
        amplifyError('UserAlreadyAuthenticatedException', 'There is already a signed in user.'),
      )
      .mockResolvedValueOnce({ isSignedIn: true, nextStep: { signInStep: 'DONE' } });
    vi.mocked(signOut).mockResolvedValue();
    vi.mocked(fetchAuthSession).mockResolvedValue({
      tokens: { idToken: { toString: () => 'id-token' } },
    } as never);
    vi.mocked(getCurrentUser).mockResolvedValue({
      username: 'user-1',
      userId: 'user-1',
      signInDetails: { loginId: 'me@example.com' },
    });

    const { result } = await renderAuth();

    await act(() => result.current.signIn('me@example.com', 'hunter22'));

    expect(signOut).toHaveBeenCalledTimes(1);
    expect(signIn).toHaveBeenCalledTimes(2);
    expect(signIn).toHaveBeenLastCalledWith({ username: 'me@example.com', password: 'hunter22' });
    expect(result.current.user).toEqual({ username: 'user-1', email: 'me@example.com' });
    expect(result.current.isAuthenticated).toBe(true);
  });

  it('rethrows other sign-in errors without signing out', async () => {
    vi.mocked(signIn).mockRejectedValue(
      amplifyError('NotAuthorizedException', 'Incorrect username or password.'),
    );

    const { result } = await renderAuth();

    await expect(result.current.signIn('me@example.com', 'wrong')).rejects.toThrow(
      'Incorrect username or password.',
    );
    expect(signOut).not.toHaveBeenCalled();
    expect(signIn).toHaveBeenCalledTimes(1);
    expect(result.current.user).toBeNull();
  });
});

describe('AuthProvider TOTP MFA', () => {
  const signedInSession = () => {
    vi.mocked(fetchAuthSession).mockResolvedValue({
      tokens: { idToken: { toString: () => 'id-token' } },
    } as never);
    vi.mocked(getCurrentUser).mockResolvedValue({
      username: 'user-1',
      userId: 'user-1',
      signInDetails: { loginId: 'me@example.com' },
    });
  };

  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(getCurrentUser).mockRejectedValueOnce(new Error('No current user'));
  });

  it('asks for a code, then signs in once the code is confirmed', async () => {
    vi.mocked(signIn).mockResolvedValue({
      isSignedIn: false,
      nextStep: { signInStep: 'CONFIRM_SIGN_IN_WITH_TOTP_CODE' },
    } as never);
    vi.mocked(confirmSignIn).mockResolvedValue({ isSignedIn: true, nextStep: { signInStep: 'DONE' } });
    const { result } = await renderAuth();

    let step: unknown;
    await act(async () => {
      step = await result.current.signIn('me@example.com', 'hunter22');
    });
    expect(step).toEqual({ step: 'totp' });
    expect(result.current.isAuthenticated).toBe(false);

    signedInSession();
    await act(() => result.current.confirmMfa('123456'));

    expect(confirmSignIn).toHaveBeenCalledWith({ challengeResponse: '123456' });
    expect(result.current.isAuthenticated).toBe(true);
  });

  it('hands back the secret and setup link when an authenticator must be set up', async () => {
    vi.mocked(signIn).mockResolvedValue({
      isSignedIn: false,
      nextStep: {
        signInStep: 'CONTINUE_SIGN_IN_WITH_TOTP_SETUP',
        totpSetupDetails: {
          sharedSecret: 'SECRET123',
          getSetupUri: (app: string, account?: string) =>
            new URL(`otpauth://totp/${app}:${account}?secret=SECRET123`),
        },
      },
    } as never);
    const { result } = await renderAuth();

    let step: unknown;
    await act(async () => {
      step = await result.current.signIn('me@example.com', 'hunter22');
    });

    expect(step).toEqual({
      step: 'totpSetup',
      sharedSecret: 'SECRET123',
      setupUri: 'otpauth://totp/BudgetTracker:me@example.com?secret=SECRET123',
    });
  });

  it('picks TOTP when Cognito asks which MFA to set up', async () => {
    vi.mocked(signIn).mockResolvedValue({
      isSignedIn: false,
      nextStep: { signInStep: 'CONTINUE_SIGN_IN_WITH_MFA_SETUP_SELECTION', allowedMFATypes: ['EMAIL', 'TOTP'] },
    } as never);
    vi.mocked(confirmSignIn).mockResolvedValue({
      isSignedIn: false,
      nextStep: { signInStep: 'CONFIRM_SIGN_IN_WITH_TOTP_CODE' },
    } as never);
    const { result } = await renderAuth();

    let step: unknown;
    await act(async () => {
      step = await result.current.signIn('me@example.com', 'hunter22');
    });

    expect(confirmSignIn).toHaveBeenCalledWith({ challengeResponse: 'TOTP' });
    expect(step).toEqual({ step: 'totp' });
  });

  it('stays signed out when the code is wrong', async () => {
    vi.mocked(signIn).mockResolvedValue({
      isSignedIn: false,
      nextStep: { signInStep: 'CONFIRM_SIGN_IN_WITH_TOTP_CODE' },
    } as never);
    vi.mocked(confirmSignIn).mockRejectedValue(amplifyError('CodeMismatchException', 'Invalid code received for user'));
    const { result } = await renderAuth();
    await act(() => result.current.signIn('me@example.com', 'hunter22'));

    await expect(result.current.confirmMfa('000000')).rejects.toThrow('Invalid code received for user');
    expect(result.current.isAuthenticated).toBe(false);
  });
});
