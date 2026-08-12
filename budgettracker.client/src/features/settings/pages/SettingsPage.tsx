import Typography from '@mui/material/Typography';
import { useUser } from '../../user/hooks/useUser';
import UserSection from '../../user/UserSection';
import LinkedAccountCard from '../../linked-accounts/components/LinkedAccountCard';

const SettingsPage = () => {
  const { isLoading: loadingUser } = useUser();

  return (
    <div className="space-y-6">
      <div>
        <Typography variant="h4" className="font-bold text-ink">
          Settings
        </Typography>
        <Typography variant="body2" className="text-ink-muted mt-1">
          Your account information
        </Typography>
      </div>

      <UserSection isLoading={loadingUser} />

      <LinkedAccountCard />
    </div>
  );
};

export default SettingsPage;
