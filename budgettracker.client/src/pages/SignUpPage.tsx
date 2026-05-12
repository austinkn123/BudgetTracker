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
import { signUpSchema, type SignUpFormData } from '../shared/validation/auth';

const SignUpPage = () => {
  const navigate = useNavigate();
  const { signUp } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { control, handleSubmit, formState: { errors } } = useForm<SignUpFormData>({
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
      <Card sx={{ width: '100%', maxWidth: 400, p: 4 }}>
        <Stack spacing={3}>
          <div>
            <Typography variant="h4" component="h1" className="font-bold text-gray-900 mb-2">
              Create Account
            </Typography>
            <Typography variant="body2" className="text-gray-500">
              Join BudgetTracker to start managing your finances
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
                name="firstName"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="First Name (optional)"
                    fullWidth
                    error={Boolean(errors.firstName)}
                    helperText={errors.firstName?.message}
                    disabled={isSubmitting}
                  />
                )}
              />

              <Controller
                name="lastName"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Last Name (optional)"
                    fullWidth
                    error={Boolean(errors.lastName)}
                    helperText={errors.lastName?.message}
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

              <Controller
                name="confirmPassword"
                control={control}
                render={({ field }) => (
                  <TextField
                    {...field}
                    label="Confirm Password"
                    type="password"
                    fullWidth
                    error={Boolean(errors.confirmPassword)}
                    helperText={errors.confirmPassword?.message}
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
                {isSubmitting ? 'Creating Account...' : 'Sign Up'}
              </Button>
            </Stack>
          </form>

          <Typography variant="body2" className="text-center text-gray-600">
            Already have an account?{' '}
            <Link to="/login" className="font-semibold text-blue-600 hover:text-blue-700">
              Sign in
            </Link>
          </Typography>
        </Stack>
      </Card>
    </Box>
  );
};

export default SignUpPage;
