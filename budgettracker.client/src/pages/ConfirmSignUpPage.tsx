import { useState, useEffect } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Wallet } from 'lucide-react';
import { Alert, Button, Card, Input } from '../shared/components/ui';
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
            <h1 className="mb-1.5 text-[22px] font-semibold tracking-[-0.02em] text-ink">Verify Email</h1>
            <p className="text-sm text-ink-muted">
              {email ? `We sent a code to ${email}` : 'Enter the confirmation code'}
            </p>
          </div>

          {error && <Alert severity="error" message={error} />}
          {success && <Alert severity="success" message={success} />}

          <form onSubmit={onSubmit}>
            <div className="flex flex-col gap-5">
              <Input
                control={control}
                name="code"
                label="Confirmation Code"
                placeholder="000000"
                maxLength={6}
                pattern="[0-9]*"
                autoComplete="one-time-code"
                disabled={isSubmitting}
              />

              <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
                {isSubmitting ? 'Confirming...' : 'Confirm'}
              </Button>
            </div>
          </form>

          <div className="flex flex-col gap-3">
            <Button
              variant="ghost"
              fullWidth
              onClick={handleResendCode}
              loading={isResending}
            >
              {isResending ? 'Resending...' : "Didn't receive a code? Resend"}
            </Button>

            <p className="text-center text-sm text-ink-muted">
              <Link to="/signup" className="font-semibold text-primary hover:text-primary-dark">
                Back to Sign Up
              </Link>
            </p>
          </div>
        </div>
        </Card>
      </div>
    </div>
  );
};

export default ConfirmSignUpPage;
