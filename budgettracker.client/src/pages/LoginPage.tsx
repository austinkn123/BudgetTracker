import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
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
    <Box
      display="flex"
      justifyContent="center"
      alignItems="center"
      minHeight="100vh"
      bgcolor="background.default"
      p={2}
    >
      <Card padding="lg" sx={{ width: '100%', maxWidth: 400 }}>
        <Stack spacing={3}>
          <div>
            <Typography variant="h4" component="h1" className="font-bold text-ink mb-2">
              Sign In
            </Typography>
            <Typography variant="body2" className="text-ink-muted">
              Welcome back to BudgetTracker
            </Typography>
          </div>

          {error && <Alert severity="error" message={error} />}

          <form onSubmit={onSubmit}>
            <Stack spacing={3}>
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
            </Stack>
          </form>

          <Stack spacing={2}>
            <Typography variant="body2" className="text-center text-ink-muted">
              Don't have an account?{' '}
              <Link to="/signup" className="font-semibold text-primary hover:text-primary-dark">
                Sign up
              </Link>
            </Typography>
            <Typography variant="body2" className="text-center text-ink-muted">
              <Link to="/forgot" className="font-semibold text-primary hover:text-primary-dark">
                Forgot password?
              </Link>
            </Typography>
          </Stack>
        </Stack>
      </Card>
    </Box>
  );
};

export default LoginPage;
