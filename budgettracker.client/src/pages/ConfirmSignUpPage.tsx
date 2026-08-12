import { useState, useEffect } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import { BudAlert, BudButton, BudCard, BudInput } from '../shared/components/ui';
import { useAuth } from '../auth/useAuth';
import { confirmSignUpSchema, type ConfirmSignUpFormData } from '../shared/validation/auth';

const ConfirmSignUpPage = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { confirmSignUp, resendCode } = useAuth();
  const [email, setEmail] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isResending, setIsResending] = useState(false);

  const { control, handleSubmit } = useForm<ConfirmSignUpFormData>({
    resolver: zodResolver(confirmSignUpSchema),
    defaultValues: {
      code: '',
    },
  });

  useEffect(() => {
    const locationEmail = (location.state as { email?: string })?.email;
    if (locationEmail) {
      setEmail(locationEmail);
    } else if (!email) {
      navigate('/signup');
    }
  }, [email, location, navigate]);

  const onSubmit = handleSubmit(async (values) => {
    if (!email) {
      setError('Email is missing. Please sign up again.');
      return;
    }

    setError(null);
    setSuccess(null);
    setIsSubmitting(true);

    try {
      await confirmSignUp(email, values.code);
      setSuccess('Email confirmed! Redirecting to sign in...');
      setTimeout(() => navigate('/login'), 2000);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Confirmation failed. Please try again.';
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  });

  const handleResendCode = async () => {
    if (!email) {
      setError('Email is missing. Please sign up again.');
      return;
    }

    setError(null);
    setSuccess(null);
    setIsResending(true);

    try {
      await resendCode(email);
      setSuccess(`Confirmation code resent to ${email}`);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to resend code. Please try again.';
      setError(message);
    } finally {
      setIsResending(false);
    }
  };

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
              Verify Email
            </Typography>
            <Typography variant="body2" className="text-ink-muted">
              {email ? `We sent a code to ${email}` : 'Enter the confirmation code'}
            </Typography>
          </div>

          {error && <BudAlert severity="error" message={error} />}
          {success && <BudAlert severity="success" message={success} />}

          <form onSubmit={onSubmit}>
            <Stack spacing={3}>
              <BudInput
                control={control}
                name="code"
                label="Confirmation Code"
                placeholder="000000"
                maxLength={6}
                pattern="[0-9]*"
                autoComplete="one-time-code"
                disabled={isSubmitting}
              />

              <BudButton type="submit" fullWidth size="lg" loading={isSubmitting}>
                {isSubmitting ? 'Confirming...' : 'Confirm'}
              </BudButton>
            </Stack>
          </form>

          <Stack spacing={2}>
            <BudButton
              variant="ghost"
              fullWidth
              onClick={handleResendCode}
              loading={isResending}
            >
              {isResending ? 'Resending...' : "Didn't receive a code? Resend"}
            </BudButton>

            <Typography variant="body2" className="text-center text-ink-muted">
              <Link to="/signup" className="font-semibold text-primary hover:text-primary-dark">
                Back to Sign Up
              </Link>
            </Typography>
          </Stack>
        </Stack>
      </BudCard>
    </Box>
  );
};

export default ConfirmSignUpPage;
