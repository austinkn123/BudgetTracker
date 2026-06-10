import { useState } from 'react';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../auth/useAuth';
import { useUser } from '../../user/hooks/useUser';
import UserSection from '../../user/UserSection';
import { StatusBanner } from '../../../shared/components/StatusBanner';
import { LinkedAccountCard } from '../../linked-accounts/components/LinkedAccountCard';

const SettingsPage = () => {
  const { isLoading: loadingUser } = useUser();
  const { signOut } = useAuth();
  const navigate = useNavigate();
  const [statusError, setStatusError] = useState<string | null>(null);
  const [isSigningOut, setIsSigningOut] = useState(false);

  const handleSignOut = async () => {
    setIsSigningOut(true);
    setStatusError(null);
    try {
      await signOut();
      navigate('/login');
    } catch (err) {
      setStatusError((err as Error).message || 'Sign out failed. Please try again.');
      setIsSigningOut(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <Typography variant="h4" className="font-bold text-gray-900">
          Settings
        </Typography>
        <Typography variant="body2" className="text-gray-500 mt-1">
          Your account information
        </Typography>
      </div>

      <UserSection isLoading={loadingUser} />

      <LinkedAccountCard />

      <div className="border rounded-lg p-4 space-y-3">
        <Typography variant="subtitle1" className="font-semibold text-gray-700">
          Session
        </Typography>
        <StatusBanner statusMessage={null} statusError={statusError} />
        <Button
          variant="outlined"
          color="error"
          onClick={handleSignOut}
          disabled={isSigningOut}
        >
          {isSigningOut ? 'Signing out…' : 'Sign out'}
        </Button>
      </div>
    </div>
  );
};

export default SettingsPage;
