import { useState, useEffect } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Input } from '../shared/components/ui';
import AuthLayout from './AuthLayout';
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
      setSuccess('Email verified. Taking you to sign in…');
      setTimeout(() => navigate('/login'), 2000);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Couldn't verify your email. Please try again.";
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
      setSuccess(`We sent a new code to ${email}.`);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Couldn't send a new code. Please try again.";
      setError(message);
    } finally {
      setIsResending(false);
    }
  };

  return (
    <AuthLayout
      title="Verify your email"
      description={
        email ? `We sent a code to ${email}.` : 'Enter the code from your verification email.'
      }
    >
      {error && <Alert severity="error" message={error} />}
      {success && <Alert severity="success" message={success} />}

      <form onSubmit={onSubmit}>
        <div className="flex flex-col gap-5">
          <Input
            control={control}
            name="code"
            label="Confirmation code"
            placeholder="000000"
            maxLength={6}
            pattern="[0-9]*"
            autoComplete="one-time-code"
            disabled={isSubmitting}
          />

          <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
            {isSubmitting ? 'Verifying…' : 'Verify email'}
          </Button>
        </div>
      </form>

      <div className="flex flex-col gap-3">
        <Button variant="ghost" fullWidth onClick={handleResendCode} loading={isResending}>
          {isResending ? 'Resending…' : "Didn't get a code? Send another"}
        </Button>

        <p className="text-center text-sm text-ink-muted">
          <Link to="/signup" className="font-semibold text-primary hover:text-primary-dark">
            Back to sign up
          </Link>
        </p>
      </div>
    </AuthLayout>
  );
};

export default ConfirmSignUpPage;
