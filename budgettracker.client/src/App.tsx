import { Navigate, Route, Routes } from 'react-router-dom';
import AppShell from './shared/components/layout/AppShell';
import DashboardPage from './features/dashboard/pages/DashboardPage';
import TransactionsPage from './features/transactions/pages/TransactionsPage';
import CashFlowPage from './features/cash-flow/pages/CashFlowPage';
import MoneyMapPage from './features/money-map/pages/MoneyMapPage';
import SettingsPage from './features/settings/pages/SettingsPage';
import InvestmentsPage from './features/webull/pages/InvestmentsPage';
import PlaidOAuthPage from './features/linked-accounts/pages/PlaidOAuthPage';
import BudgetBuilderPage from './features/budget-builder/pages/BudgetBuilderPage';
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
          <Route path="/" element={<MoneyMapPage />} />
          <Route path="/budget" element={<BudgetBuilderPage />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/cash-flow" element={<CashFlowPage />} />
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/investments" element={<InvestmentsPage />} />
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