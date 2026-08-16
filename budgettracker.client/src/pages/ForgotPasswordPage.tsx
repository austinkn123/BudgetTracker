import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Wallet } from 'lucide-react';
import { Alert, Button, Card, Input } from '../shared/components/ui';
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
      const message = err instanceof Error ? err.message : 'Failed to send reset code. Please try again.';
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
      setSuccess('Password reset successful! Redirecting to sign in...');
      setTimeout(() => navigate('/login'), 2000);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Password reset failed. Please try again.';
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  });

  return (
    <div className="flex min-h-screen items-center justify-center bg-grey-900 p-4">
      <div className="w-full max-w-[400px]">
        <div className="mb-6 flex items-center justify-center gap-2.5">
          <span className="flex text-primary-light">
            <Wallet size={24} />
          </span>
          <span className="text-[17px] font-semibold tracking-[-0.01em] text-white">
            BudgetTracker
          </span>
        </div>
        <Card padding="lg">
        <div className="flex flex-col gap-6">
          <div>
            <h1 className="mb-1.5 text-[22px] font-semibold tracking-[-0.02em] text-ink">Reset Password</h1>
            <p className="text-sm text-ink-muted">
              {step === 'request'
                ? 'Enter your email to receive a reset code'
                : `Confirm the code sent to ${email}`}
            </p>
          </div>

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
                  {isSubmitting ? 'Sending Code...' : 'Send Reset Code'}
                </Button>
              </div>
            </form>
          ) : (
            <form onSubmit={handleResetPassword}>
              <div className="flex flex-col gap-5">
                <Input
                  control={resetForm.control}
                  name="code"
                  label="Confirmation Code"
                  placeholder="000000"
                  maxLength={6}
                  pattern="[0-9]*"
                  autoComplete="one-time-code"
                  disabled={isSubmitting}
                />

                <Input
                  control={resetForm.control}
                  name="newPassword"
                  label="New Password"
                  type="password"
                  autoComplete="new-password"
                  disabled={isSubmitting}
                />

                <Input
                  control={resetForm.control}
                  name="confirmPassword"
                  label="Confirm Password"
                  type="password"
                  autoComplete="new-password"
                  disabled={isSubmitting}
                />

                <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
                  {isSubmitting ? 'Resetting Password...' : 'Reset Password'}
                </Button>
              </div>
            </form>
          )}

          <p className="text-center text-sm text-ink-muted">
            <Link to="/login" className="font-semibold text-primary hover:text-primary-dark">
              Back to Sign In
            </Link>
          </p>
        </div>
        </Card>
      </div>
    </div>
  );
};

export default ForgotPasswordPage;
