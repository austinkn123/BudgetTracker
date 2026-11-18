import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import LoginPage from './LoginPage';

const signIn = vi.fn();
const confirmMfa = vi.fn();
vi.mock('../auth/useAuth', () => ({ useAuth: () => ({ signIn, confirmMfa }) }));

const renderLogin = () =>
  render(
    <MemoryRouter initialEntries={['/login']}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<p>Dashboard</p>} />
      </Routes>
    </MemoryRouter>,
  );

const submitPassword = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(screen.getByLabelText('Email'), 'me@example.com');
  await user.type(screen.getByLabelText('Password'), 'hunter22');
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
};

describe('LoginPage', () => {
  beforeEach(() => vi.resetAllMocks());

  it('goes straight to the dashboard when no code is needed', async () => {
    const user = userEvent.setup();
    signIn.mockResolvedValue({ step: 'done' });
    renderLogin();

    await submitPassword(user);

    expect(await screen.findByText('Dashboard')).toBeInTheDocument();
  });

  it('asks for the authenticator code, then signs in', async () => {
    const user = userEvent.setup();
    signIn.mockResolvedValue({ step: 'totp' });
    confirmMfa.mockResolvedValue(undefined);
    renderLogin();

    await submitPassword(user);
    await user.type(await screen.findByLabelText('6-digit code'), '123456');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    await waitFor(() => expect(confirmMfa).toHaveBeenCalledWith('123456'));
    expect(await screen.findByText('Dashboard')).toBeInTheDocument();
  });

  it('rejects a code that is not 6 digits without calling Cognito', async () => {
    const user = userEvent.setup();
    signIn.mockResolvedValue({ step: 'totp' });
    renderLogin();

    await submitPassword(user);
    await user.type(await screen.findByLabelText('6-digit code'), '12ab');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    expect(await screen.findByText('Enter the 6-digit code')).toBeInTheDocument();
    expect(confirmMfa).not.toHaveBeenCalled();
  });

  it('shows the setup key and link the first time', async () => {
    const user = userEvent.setup();
    signIn.mockResolvedValue({
      step: 'totpSetup',
      sharedSecret: 'ABCDEFGH',
      setupUri: 'otpauth://totp/BudgetTracker:me@example.com?secret=ABCDEFGH',
    });
    renderLogin();

    await submitPassword(user);

    expect(await screen.findByText('ABCD EFGH')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open in authenticator app' })).toHaveAttribute(
      'href',
      'otpauth://totp/BudgetTracker:me@example.com?secret=ABCDEFGH',
    );
  });

  it('shows the error and stays on the code step when the code is wrong', async () => {
    const user = userEvent.setup();
    signIn.mockResolvedValue({ step: 'totp' });
    confirmMfa.mockRejectedValue(new Error('Invalid code received for user'));
    renderLogin();

    await submitPassword(user);
    await user.type(await screen.findByLabelText('6-digit code'), '000000');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    expect(await screen.findByText('Invalid code received for user')).toBeInTheDocument();
    expect(screen.getByLabelText('6-digit code')).toBeInTheDocument();
  });
});
