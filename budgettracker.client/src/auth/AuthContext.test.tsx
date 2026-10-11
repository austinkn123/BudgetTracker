import type { ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { fetchAuthSession, getCurrentUser, signIn, signOut } from 'aws-amplify/auth';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthProvider } from './AuthContext';
import { useAuth } from './useAuth';

vi.mock('aws-amplify/auth', () => ({
  getCurrentUser: vi.fn(),
  fetchAuthSession: vi.fn(),
  signIn: vi.fn(),
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
