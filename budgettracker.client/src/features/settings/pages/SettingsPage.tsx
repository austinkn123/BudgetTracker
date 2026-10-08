import { PageHeader } from '../../../shared/components/ui';
import { useUser } from '../../user/hooks/useUser';
import UserSection from '../../user/UserSection';
import LinkedAccountsSection from '../../linked-accounts/components/LinkedAccountsSection';
import AppearanceSection from '../components/AppearanceSection';

const SettingsPage = () => {
  const { isLoading: loadingUser } = useUser();

  return (
    <div className="stagger-in space-y-6">
      <PageHeader title="Settings" description="Your account, how the app looks, and your linked bank." />

      <UserSection isLoading={loadingUser} />

      <AppearanceSection />

      <LinkedAccountsSection />
    </div>
  );
};

export default SettingsPage;
