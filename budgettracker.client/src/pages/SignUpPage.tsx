import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Input } from '../shared/components/ui';
import AuthLayout from './AuthLayout';
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
      const message =
        err instanceof Error ? err.message : "Couldn't create your account. Please try again.";
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  });

  return (
    <AuthLayout
      title="Create your account"
      description="Set up a plan, link your bank, and see each month against it."
    >
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
            label="First name (optional)"
            autoComplete="given-name"
            disabled={isSubmitting}
          />

          <Input
            control={control}
            name="lastName"
            label="Last name (optional)"
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
            label="Confirm password"
            type="password"
            autoComplete="new-password"
            disabled={isSubmitting}
          />

          <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
            {isSubmitting ? 'Creating account…' : 'Create account'}
          </Button>
        </div>
      </form>

      <p className="text-center text-sm text-ink-muted">
        Already have an account?{' '}
        <Link to="/login" className="font-semibold text-primary hover:text-primary-dark">
          Sign in
        </Link>
      </p>
    </AuthLayout>
  );
};

export default SignUpPage;
