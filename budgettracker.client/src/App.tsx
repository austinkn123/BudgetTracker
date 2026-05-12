import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from './shared/components/Layout';
import DashboardPage from './features/dashboard/pages/DashboardPage';
import TransactionsPage from './features/transactions/pages/TransactionsPage';
import BudgetPlansPage from './features/budget-plans/pages/BudgetPlansPage';
import SettingsPage from './features/settings/pages/SettingsPage';
import LoginPage from './pages/LoginPage';
import SignUpPage from './pages/SignUpPage';
import ConfirmSignUpPage from './pages/ConfirmSignUpPage';
import ForgotPasswordPage from './pages/ForgotPasswordPage';
import { ProtectedRoute } from './auth/ProtectedRoute';

function App() {
  return (
    <Routes>
      {/* Public auth routes */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/signup" element={<SignUpPage />} />
      <Route path="/confirm" element={<ConfirmSignUpPage />} />
      <Route path="/forgot" element={<ForgotPasswordPage />} />

      {/* Protected app routes */}
      <Route element={<ProtectedRoute />}>
        <Route element={<Layout />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/transactions" element={<TransactionsPage />} />
          <Route path="/budget-plans" element={<BudgetPlansPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
      </Route>

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default App;