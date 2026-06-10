import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
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
import { loginSchema, type LoginFormData } from '../shared/validation/auth';

const LoginPage = () => {
  const navigate = useNavigate();
  const { signIn } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { control, handleSubmit, formState: { errors } } = useForm<LoginFormData>({
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
      <Card sx={{ width: '100%', maxWidth: 400, p: 4 }}>
        <Stack spacing={3}>
          <div>
            <Typography variant="h4" component="h1" className="font-bold text-ink mb-2">
              Sign In
            </Typography>
            <Typography variant="body2" className="text-ink-muted">
              Welcome back to BudgetTracker
            </Typography>
          </div>

          {error && <Alert severity="error">{error}</Alert>}

          <form onSubmit={onSubmit}>
            <Stack spacing={3}>
              <Controller
                name="email"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Email"
                    type="email"
                    fullWidth
                    error={Boolean(errors.email)}
                    helperText={errors.email?.message}
                    disabled={isSubmitting}
                  />
                )}
              />

              <Controller
                name="password"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Password"
                    type="password"
                    fullWidth
                    error={Boolean(errors.password)}
                    helperText={errors.password?.message}
                    disabled={isSubmitting}
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
