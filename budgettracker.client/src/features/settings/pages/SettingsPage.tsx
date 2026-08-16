import { PageHeader } from '../../../shared/components/ui';
import { useUser } from '../../user/hooks/useUser';
import UserSection from '../../user/UserSection';
import LinkedAccountCard from '../../linked-accounts/components/LinkedAccountCard';

const SettingsPage = () => {
  const { isLoading: loadingUser } = useUser();

  return (
    <div className="space-y-6">
      <PageHeader title="Settings" description="Your account information" />

      <UserSection isLoading={loadingUser} />

      <LinkedAccountCard />
    </div>
  );
};

export default SettingsPage;
