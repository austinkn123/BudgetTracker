import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Wallet } from 'lucide-react';
import { Alert, Button, Card, Input } from '../shared/components/ui';
import { useAuth } from '../auth/useAuth';
import { signUpSchema, type SignUpFormData } from '../shared/validation/auth';

const SignUpPage = () => {
  const navigate = useNavigate();
  const { signUp } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { control, handleSubmit } = useForm<SignUpFormData>({
    resolver: zodResolver(signUpSchema),
    defaultValues: {
      email: '',
      password: '',
      confirmPassword: '',
      firstName: '',
      lastName: '',
    },
  });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    setIsSubmitting(true);

    try {
      await signUp(values.email, values.password, values.firstName, values.lastName);
      navigate('/confirm', { state: { email: values.email } });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Sign up failed. Please try again.';
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  });

  return (
    <div className="flex min-h-screen items-center justify-center bg-grey-900 p-4 dark:bg-background">
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
            <h1 className="mb-1.5 text-[22px] font-semibold tracking-[-0.02em] text-ink">Create Account</h1>
            <p className="text-sm text-ink-muted">
              Join BudgetTracker to start managing your finances
            </p>
          </div>

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
                name="firstName"
                label="First Name (optional)"
                autoComplete="given-name"
                disabled={isSubmitting}
              />

              <Input
                control={control}
                name="lastName"
                label="Last Name (optional)"
                autoComplete="family-name"
                disabled={isSubmitting}
              />

              <Input
                control={control}
                name="password"
                label="Password"
                type="password"
                autoComplete="new-password"
                disabled={isSubmitting}
              />

              <Input
                control={control}
                name="confirmPassword"
                label="Confirm Password"
                type="password"
                autoComplete="new-password"
                disabled={isSubmitting}
              />

              <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
                {isSubmitting ? 'Creating Account...' : 'Sign Up'}
              </Button>
            </div>
          </form>

          <p className="text-center text-sm text-ink-muted">
            Already have an account?{' '}
            <Link to="/login" className="font-semibold text-primary hover:text-primary-dark">
              Sign in
            </Link>
          </p>
        </div>
        </Card>
      </div>
    </div>
  );
};

export default SignUpPage;
