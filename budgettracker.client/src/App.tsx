import { Navigate, Route, Routes } from 'react-router-dom';
import AppShell from './shared/components/layout/AppShell';
import DashboardPage from './features/dashboard/pages/DashboardPage';
import TransactionsPage from './features/transactions/pages/TransactionsPage';
import BudgetPlansPage from './features/budget-plans/pages/BudgetPlansPage';
import SettingsPage from './features/settings/pages/SettingsPage';
import PlaidOAuthPage from './features/linked-accounts/pages/PlaidOAuthPage';
import LoginPage from './pages/LoginPage';
import ForgotPasswordPage from './pages/ForgotPasswordPage';
import ProtectedRoute from './auth/ProtectedRoute';

function App() {
  return (
    <Routes>
      {/* Public auth routes */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forgot" element={<ForgotPasswordPage />} />

      {/* Protected app routes */}
      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/budget-plans" element={<BudgetPlansPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          {/* Plaid's OAuth redirect target (Plaid:RedirectUri) when a bank can't open in a popup. */}
          <Route path="/plaid-oauth" element={<PlaidOAuthPage />} />
        </Route>
      </Route>

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default App;