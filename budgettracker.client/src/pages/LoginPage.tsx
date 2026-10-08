import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Input } from '../shared/components/ui';
import AuthLayout from './AuthLayout';
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
      // Cognito's messages are written for people ("Incorrect username or password."), so they show as-is.
      const message =
        err instanceof Error ? err.message : "Couldn't sign you in. Please try again.";
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  });

  return (
    <AuthLayout title="Sign in" description="Welcome back. Pick up where the month left off.">
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
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </Button>
        </div>
      </form>

      <div className="flex flex-col gap-3">
        <p className="text-center text-sm text-ink-muted">
          <Link to="/forgot" className="font-semibold text-primary hover:text-primary-dark">
            Forgot password?
          </Link>
        </p>
      </div>
    </AuthLayout>
  );
};

export default LoginPage;
