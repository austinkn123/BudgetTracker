import Typography from '@mui/material/Typography';
import { useUser } from '../../user/hooks/useUser';
import UserSection from '../../user/UserSection';

const SettingsPage = () => {
  const { isLoading: loadingUser } = useUser();

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
    </div>
  );
};

export default SettingsPage;
