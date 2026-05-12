import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './useAuth';
import CircularProgress from '@mui/material/CircularProgress';
import Box from '@mui/material/Box';

export const ProtectedRoute = () => {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        minHeight="100vh"
      >
        <CircularProgress />
      </Box>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
};
