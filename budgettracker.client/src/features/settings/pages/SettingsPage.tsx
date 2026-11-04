import { PageHeader } from '../../../shared/components/ui';
import LinkedAccountsSection from '../../linked-accounts/components/LinkedAccountsSection';
import WebullSection from '../../webull/components/WebullSection';
import AppearanceSection from '../components/AppearanceSection';

const SettingsPage = () => {
  return (
    <div className="stagger-in space-y-6">
      <PageHeader title="Settings" description="How the app looks, and the accounts it reads from." />

      <AppearanceSection />

      <LinkedAccountsSection />

      <WebullSection />
    </div>
  );
};

export default SettingsPage;
