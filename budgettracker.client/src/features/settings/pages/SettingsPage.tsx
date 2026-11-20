import { PageHeader } from '../../../shared/components/ui';
import CategoryRulesSection from '../../categories/components/CategoryRulesSection';
import LinkedAccountsSection from '../../linked-accounts/components/LinkedAccountsSection';
import WebullSection from '../../webull/components/WebullSection';
import AppearanceSection from '../components/AppearanceSection';

const SettingsPage = () => {
  return (
    <div className="stagger-in space-y-6">
      <PageHeader title="Settings" description="How the app looks, the accounts it reads from, and how imports are categorized." />

      <AppearanceSection />

      <LinkedAccountsSection />

      <CategoryRulesSection />

      <WebullSection />
    </div>
  );
};

export default SettingsPage;
