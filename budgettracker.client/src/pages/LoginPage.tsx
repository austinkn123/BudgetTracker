import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Wallet } from 'lucide-react';
import { Alert, Button, Card, Input } from '../shared/components/ui';
import { useAuth } from '../auth/useAuth';
import { loginSchema, type LoginFormData } from '../shared/validation/auth';

const LoginPage = () => {
  const navigate = useNavigate();
  const { signIn } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { control, handleSubmit } = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  const onSubmit = handleSubmit(async (values) => {
    setError(null);
    setIsSubmitting(true);

    try {
      await signIn(values.email, values.password);
      navigate('/');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Sign in failed. Please try again.';
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
            <h1 className="mb-1.5 text-[22px] font-semibold tracking-[-0.02em] text-ink">Sign In</h1>
            <p className="text-sm text-ink-muted">Welcome back to BudgetTracker</p>
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
                name="password"
                label="Password"
                type="password"
                autoComplete="current-password"
                disabled={isSubmitting}
              />

              <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
                {isSubmitting ? 'Signing In...' : 'Sign In'}
              </Button>
            </div>
          </form>

          <div className="flex flex-col gap-3">
            <p className="text-center text-sm text-ink-muted">
              Don't have an account?{' '}
              <Link to="/signup" className="font-semibold text-primary hover:text-primary-dark">
                Sign up
              </Link>
            </p>
            <p className="text-center text-sm text-ink-muted">
              <Link to="/forgot" className="font-semibold text-primary hover:text-primary-dark">
                Forgot password?
              </Link>
            </p>
          </div>
        </div>
        </Card>
      </div>
    </div>
  );
};

export default LoginPage;
