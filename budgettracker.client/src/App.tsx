import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from './shared/components/Layout';
import DashboardPage from './features/dashboard/pages/DashboardPage';
import TransactionsPage from './features/transactions/pages/TransactionsPage';
import BudgetPlansPage from './features/budget-plans/pages/BudgetPlansPage';
import SettingsPage from './features/settings/pages/SettingsPage';

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/transactions" element={<TransactionsPage />} />
        <Route path="/budget-plans" element={<BudgetPlansPage />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}

export default App;