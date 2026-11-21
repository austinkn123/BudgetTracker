import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Input } from '../shared/components/ui';
import AuthLayout from './AuthLayout';
import { useAuth } from '../auth/useAuth';
import type { SignInResult } from '../auth/authContextValue';
import {
  loginSchema,
  mfaCodeSchema,
  type LoginFormData,
  type MfaCodeFormData,
} from '../shared/validation/auth';

// Cognito's messages are written for people ("Incorrect username or password."), so they show as-is.
const messageOf = (err: unknown) =>
  err instanceof Error ? err.message : "Couldn't sign you in. Please try again.";

/** The setup key in groups of four, the way authenticator apps show it. */
const groupKey = (key: string) => key.match(/.{1,4}/g)?.join(' ') ?? key;

const LoginPage = () => {
  const navigate = useNavigate();
  const { signIn, confirmMfa } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  // Set once the password is accepted and Cognito wants an authenticator code.
  const [mfaStep, setMfaStep] = useState<Exclude<SignInResult, { step: 'done' }> | null>(null);

  const { control, handleSubmit, resetField } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  const codeForm = useForm<MfaCodeFormData>({
    resolver: zodResolver(mfaCodeSchema),
    defaultValues: { code: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    setIsSubmitting(true);

    try {
      const result = await signIn(values.email, values.password);
      if (result.step === 'done') navigate('/');
      else setMfaStep(result);
    } catch (err) {
      setError(messageOf(err));
    } finally {
      setIsSubmitting(false);
    }
  });

  const onSubmitCode = codeForm.handleSubmit(async ({ code }) => {
    setError(null);
    setIsSubmitting(true);

    try {
      await confirmMfa(code);
      navigate('/');
    } catch (err) {
      setError(messageOf(err));
    } finally {
      setIsSubmitting(false);
    }
  });

  // A wrong or expired code would otherwise leave reloading as the only way out. Keeps the email, drops the password.
  const backToSignIn = () => {
    setMfaStep(null);
    setError(null);
    codeForm.reset();
    resetField('password');
  };

  if (mfaStep) {
    const isSetup = mfaStep.step === 'totpSetup';
    return (
      <AuthLayout
        title={isSetup ? 'Set up your authenticator' : 'Enter your code'}
        description={
          isSetup
            ? 'Add BudgetTracker to an authenticator app, then enter the code it shows.'
            : 'Open your authenticator app and enter the code for BudgetTracker.'
        }
      >
        {error && <Alert severity="error" message={error} />}

        {isSetup && (
          <div className="flex flex-col gap-2 rounded-lg border border-border bg-background p-4 text-sm">
            <p className="text-ink-muted">Setup key</p>
            <p className="numeric select-all break-all font-semibold tracking-wide text-ink">
              {groupKey(mfaStep.sharedSecret)}
            </p>
            <a href={mfaStep.setupUri} className="font-semibold text-primary hover:text-primary-dark">
              Open in authenticator app
            </a>
          </div>
        )}

        <form onSubmit={onSubmitCode}>
          <div className="flex flex-col gap-5">
            <Input
              control={codeForm.control}
              name="code"
              label="6-digit code"
              type="text"
              autoComplete="one-time-code"
              autoFocus
              maxLength={6}
              disabled={isSubmitting}
            />

            <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
              {isSubmitting ? 'Verifying…' : 'Verify'}
            </Button>
            <Button variant="ghost" fullWidth onClick={backToSignIn} disabled={isSubmitting}>
              Back to sign in
            </Button>
          </div>
        </form>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title="Sign in" description="Welcome back. Pick up where the month left off.">
      {error && <Alert severity="error" message={error} />}

      <form onSubmit={onSubmit}>
        <div className="flex flex-col gap-5">
          <Input
            control={control}
            name="email"
            label="Email"
            type="email"
            autoComplete="email"
            disabled={isSubmitting}
          />

          <Input
            control={control}
            name="password"
            label="Password"
            type="password"
            autoComplete="current-password"
            disabled={isSubmitting}
          />

          <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </Button>
        </div>
      </form>

      <div className="flex flex-col gap-3">
        <p className="text-center text-sm text-ink-muted">
          <Link to="/forgot" className="touch-target font-semibold text-primary hover:text-primary-dark">
            Forgot password?
          </Link>
        </p>
      </div>
    </AuthLayout>
  );
};

export default LoginPage;
