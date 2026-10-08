import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Input } from '../shared/components/ui';
import AuthLayout from './AuthLayout';
import { useAuth } from '../auth/useAuth';
import {
  forgotPasswordSchema,
  resetPasswordSchema,
  type ForgotPasswordFormData,
  type ResetPasswordFormData,
} from '../shared/validation/auth';

type Step = 'request' | 'reset';

const ForgotPasswordPage = () => {
  const navigate = useNavigate();
  const { forgotPassword, confirmForgotPassword } = useAuth();
  const [step, setStep] = useState<Step>('request');
  const [email, setEmail] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const requestForm = useForm<ForgotPasswordFormData>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: {
      email: '',
    },
  });

  const resetForm = useForm<ResetPasswordFormData>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: {
      code: '',
      newPassword: '',
      confirmPassword: '',
    },
  });

  const handleRequestCode = requestForm.handleSubmit(async (values) => {
    setError(null);
    setSuccess(null);
    setIsSubmitting(true);

    try {
      await forgotPassword(values.email);
      setEmail(values.email);
      setSuccess(`Confirmation code sent to ${values.email}`);
      setStep('reset');
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Couldn't send a reset code. Please try again.";
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  });

  const handleResetPassword = resetForm.handleSubmit(async (values) => {
    if (!email) {
      setError('Email is missing. Please start over.');
      return;
    }

    setError(null);
    setSuccess(null);
    setIsSubmitting(true);

    try {
      await confirmForgotPassword(email, values.code, values.newPassword);
      setSuccess('Password updated. Taking you to sign in…');
      setTimeout(() => navigate('/login'), 2000);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Couldn't reset your password. Please try again.";
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  });

  return (
    <AuthLayout
      title="Reset your password"
      description={
        step === 'request'
          ? "Enter your email and we'll send you a reset code."
          : `Enter the code we sent to ${email}.`
      }
    >
      {error && <Alert severity="error" message={error} />}
      {success && <Alert severity="success" message={success} />}

      {step === 'request' ? (
        <form onSubmit={handleRequestCode}>
          <div className="flex flex-col gap-5">
            <Input
              control={requestForm.control}
              name="email"
              label="Email"
              type="email"
              autoComplete="email"
              disabled={isSubmitting}
            />

            <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
              {isSubmitting ? 'Sending code…' : 'Send reset code'}
            </Button>
          </div>
        </form>
      ) : (
        <form onSubmit={handleResetPassword}>
          <div className="flex flex-col gap-5">
            <Input
              control={resetForm.control}
              name="code"
              label="Confirmation code"
              placeholder="000000"
              maxLength={6}
              pattern="[0-9]*"
              autoComplete="one-time-code"
              disabled={isSubmitting}
            />

            <Input
              control={resetForm.control}
              name="newPassword"
              label="New password"
              type="password"
              autoComplete="new-password"
              disabled={isSubmitting}
            />

            <Input
              control={resetForm.control}
              name="confirmPassword"
              label="Confirm password"
              type="password"
              autoComplete="new-password"
              disabled={isSubmitting}
            />

            <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
              {isSubmitting ? 'Resetting password…' : 'Reset password'}
            </Button>
          </div>
        </form>
      )}

      <p className="text-center text-sm text-ink-muted">
        <Link to="/login" className="font-semibold text-primary hover:text-primary-dark">
          Back to sign in
        </Link>
      </p>
    </AuthLayout>
  );
};

export default ForgotPasswordPage;
