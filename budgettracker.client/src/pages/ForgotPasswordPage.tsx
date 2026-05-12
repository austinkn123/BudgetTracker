import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import Alert from '@mui/material/Alert';
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
      <Card sx={{ width: '100%', maxWidth: 400, p: 4 }}>
        <Stack spacing={3}>
          <div>
            <Typography variant="h4" component="h1" className="font-bold text-gray-900 mb-2">
              Reset Password
            </Typography>
            <Typography variant="body2" className="text-gray-500">
              {step === 'request'
                ? 'Enter your email to receive a reset code'
                : `Confirm the code sent to ${email}`}
            </Typography>
          </div>

          {error && <Alert severity="error">{error}</Alert>}
          {success && <Alert severity="success">{success}</Alert>}

          {step === 'request' ? (
            <form onSubmit={handleRequestCode}>
              <Stack spacing={3}>
                <Controller
                  name="email"
                  control={requestForm.control}
                  render={({ field }) => (
                    <TextField
                      {...field}
                      label="Email"
                      type="email"
                      fullWidth
                      error={Boolean(requestForm.formState.errors.email)}
                      helperText={requestForm.formState.errors.email?.message}
                      disabled={isSubmitting}
                    />
                  )}
                />

                <Button
                  type="submit"
                  variant="contained"
                  fullWidth
                  size="large"
                  disabled={isSubmitting}
                >
                  {isSubmitting ? 'Sending Code...' : 'Send Reset Code'}
                </Button>
              </Stack>
            </form>
          ) : (
            <form onSubmit={handleResetPassword}>
              <Stack spacing={3}>
                <Controller
                  name="code"
                  control={resetForm.control}
                  render={({ field }) => (
                    <TextField
                      {...field}
                      label="Confirmation Code"
                      placeholder="000000"
                      fullWidth
                      inputProps={{ maxLength: 6, pattern: '[0-9]*' }}
                      error={Boolean(resetForm.formState.errors.code)}
                      helperText={resetForm.formState.errors.code?.message}
                      disabled={isSubmitting}
                      autoComplete="one-time-code"
                    />
                  )}
                />

                <Controller
                  name="newPassword"
                  control={resetForm.control}
                  render={({ field }) => (
                    <TextField
                      {...field}
                      label="New Password"
                      type="password"
                      fullWidth
                      error={Boolean(resetForm.formState.errors.newPassword)}
                      helperText={resetForm.formState.errors.newPassword?.message}
                      disabled={isSubmitting}
                    />
                  )}
                />

                <Controller
                  name="confirmPassword"
                  control={resetForm.control}
                  render={({ field }) => (
                    <TextField
                      {...field}
                      label="Confirm Password"
                      type="password"
                      fullWidth
                      error={Boolean(resetForm.formState.errors.confirmPassword)}
                      helperText={resetForm.formState.errors.confirmPassword?.message}
                      disabled={isSubmitting}
                    />
                  )}
                />

                <Button
                  type="submit"
                  variant="contained"
                  fullWidth
                  size="large"
                  disabled={isSubmitting}
                >
                  {isSubmitting ? 'Resetting Password...' : 'Reset Password'}
                </Button>
              </Stack>
            </form>
          )}

          <Typography variant="body2" className="text-center text-gray-600">
            <Link to="/login" className="font-semibold text-blue-600 hover:text-blue-700">
              Back to Sign In
            </Link>
          </Typography>
        </Stack>
      </Card>
    </Box>
  );
};

export default ForgotPasswordPage;
