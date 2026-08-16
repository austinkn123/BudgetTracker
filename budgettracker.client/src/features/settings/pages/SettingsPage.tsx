import { useUser } from '../../user/hooks/useUser';
import UserSection from '../../user/UserSection';
import LinkedAccountCard from '../../linked-accounts/components/LinkedAccountCard';

const SettingsPage = () => {
  const { isLoading: loadingUser } = useUser();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-ink">Settings</h1>
        <p className="mt-1 text-sm text-ink-muted">Your account information</p>
      </div>

      <UserSection isLoading={loadingUser} />

      <LinkedAccountCard />
    </div>
  );
};

export default SettingsPage;
