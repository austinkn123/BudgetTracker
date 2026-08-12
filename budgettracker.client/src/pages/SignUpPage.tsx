import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import { BudAlert, BudButton, BudCard, BudInput } from '../shared/components/ui';
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
              Create Account
            </Typography>
            <Typography variant="body2" className="text-ink-muted">
              Join BudgetTracker to start managing your finances
            </Typography>
          </div>

          {error && <BudAlert severity="error" message={error} />}

          <form onSubmit={onSubmit}>
            <Stack spacing={3}>
              <BudInput
                control={control}
                name="email"
                label="Email"
                type="email"
                autoComplete="email"
                disabled={isSubmitting}
              />

              <BudInput
                control={control}
                name="firstName"
                label="First Name (optional)"
                autoComplete="given-name"
                disabled={isSubmitting}
              />

              <BudInput
                control={control}
                name="lastName"
                label="Last Name (optional)"
                autoComplete="family-name"
                disabled={isSubmitting}
              />

              <BudInput
                control={control}
                name="password"
                label="Password"
                type="password"
                autoComplete="new-password"
                disabled={isSubmitting}
              />

              <BudInput
                control={control}
                name="confirmPassword"
                label="Confirm Password"
                type="password"
                autoComplete="new-password"
                disabled={isSubmitting}
              />

              <BudButton type="submit" fullWidth size="lg" loading={isSubmitting}>
                {isSubmitting ? 'Creating Account...' : 'Sign Up'}
              </BudButton>
            </Stack>
          </form>

          <Typography variant="body2" className="text-center text-ink-muted">
            Already have an account?{' '}
            <Link to="/login" className="font-semibold text-primary hover:text-primary-dark">
              Sign in
            </Link>
          </Typography>
        </Stack>
      </BudCard>
    </Box>
  );
};

export default SignUpPage;
