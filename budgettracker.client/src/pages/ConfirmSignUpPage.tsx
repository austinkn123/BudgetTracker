import { useState, useEffect } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
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

  const { control, handleSubmit, formState: { errors } } = useForm<ConfirmSignUpFormData>({
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
      <Card sx={{ width: '100%', maxWidth: 400, p: 4 }}>
        <Stack spacing={3}>
          <div>
            <Typography variant="h4" component="h1" className="font-bold text-ink mb-2">
              Verify Email
            </Typography>
            <Typography variant="body2" className="text-ink-muted">
              {email ? `We sent a code to ${email}` : 'Enter the confirmation code'}
            </Typography>
          </div>

          {error && <Alert severity="error">{error}</Alert>}
          {success && <Alert severity="success">{success}</Alert>}

          <form onSubmit={onSubmit}>
            <Stack spacing={3}>
              <Controller
                name="code"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Confirmation Code"
                    placeholder="000000"
                    fullWidth
                    inputProps={{ maxLength: 6, pattern: '[0-9]*' }}
                    error={Boolean(errors.code)}
                    helperText={errors.code?.message}
                    disabled={isSubmitting}
                    autoComplete="one-time-code"
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
                {isSubmitting ? 'Confirming...' : 'Confirm'}
              </Button>
            </Stack>
          </form>

          <Stack spacing={2}>
            <Button
              variant="text"
              fullWidth
              onClick={handleResendCode}
              disabled={isResending}
            >
              {isResending ? 'Resending...' : "Didn't receive a code? Resend"}
            </Button>

            <Typography variant="body2" className="text-center text-ink-muted">
              <Link to="/signup" className="font-semibold text-primary hover:text-primary-dark">
                Back to Sign Up
              </Link>
            </Typography>
          </Stack>
        </Stack>
      </Card>
    </Box>
  );
};

export default ConfirmSignUpPage;
