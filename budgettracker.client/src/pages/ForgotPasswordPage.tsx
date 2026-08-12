import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import { BudAlert, BudButton, BudCard, BudInput } from '../shared/components/ui';
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
    <Box
      display="flex"
      justifyContent="center"
      alignItems="center"
      minHeight="100vh"
      bgcolor="background.default"
      p={2}
    >
      <BudCard padding="lg" sx={{ width: '100%', maxWidth: 400 }}>
        <Stack spacing={3}>
          <div>
            <Typography variant="h4" component="h1" className="font-bold text-ink mb-2">
              Reset Password
            </Typography>
            <Typography variant="body2" className="text-ink-muted">
              {step === 'request'
                ? 'Enter your email to receive a reset code'
                : `Confirm the code sent to ${email}`}
            </Typography>
          </div>

          {error && <BudAlert severity="error" message={error} />}
          {success && <BudAlert severity="success" message={success} />}

          {step === 'request' ? (
            <form onSubmit={handleRequestCode}>
              <Stack spacing={3}>
                <BudInput
                  control={requestForm.control}
                  name="email"
                  label="Email"
                  type="email"
                  autoComplete="email"
                  disabled={isSubmitting}
                />

                <BudButton type="submit" fullWidth size="lg" loading={isSubmitting}>
                  {isSubmitting ? 'Sending Code...' : 'Send Reset Code'}
                </BudButton>
              </Stack>
            </form>
          ) : (
            <form onSubmit={handleResetPassword}>
              <Stack spacing={3}>
                <BudInput
                  control={resetForm.control}
                  name="code"
                  label="Confirmation Code"
                  placeholder="000000"
                  maxLength={6}
                  pattern="[0-9]*"
                  autoComplete="one-time-code"
                  disabled={isSubmitting}
                />

                <BudInput
                  control={resetForm.control}
                  name="newPassword"
                  label="New Password"
                  type="password"
                  autoComplete="new-password"
                  disabled={isSubmitting}
                />

                <BudInput
                  control={resetForm.control}
                  name="confirmPassword"
                  label="Confirm Password"
                  type="password"
                  autoComplete="new-password"
                  disabled={isSubmitting}
                />

                <BudButton type="submit" fullWidth size="lg" loading={isSubmitting}>
                  {isSubmitting ? 'Resetting Password...' : 'Reset Password'}
                </BudButton>
              </Stack>
            </form>
          )}

          <Typography variant="body2" className="text-center text-ink-muted">
            <Link to="/login" className="font-semibold text-primary hover:text-primary-dark">
              Back to Sign In
            </Link>
          </Typography>
        </Stack>
      </BudCard>
    </Box>
  );
};

export default ForgotPasswordPage;
