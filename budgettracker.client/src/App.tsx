import { lazy, Suspense, type ReactNode } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import AppShell from './shared/components/layout/AppShell';
import PageSkeleton from './shared/components/layout/PageSkeleton';
import ProtectedRoute from './auth/ProtectedRoute';

// Every page loads on demand (BUD-28): signing in downloads the shell and the page you land on,
// not the whole app. A failed download (e.g. a stale tab after a deploy) reaches the shell's
// ErrorBoundary, which offers a reload.
const TransactionsPage = lazy(() => import('./features/transactions/pages/TransactionsPage'));
const MoneyMapPage = lazy(() => import('./features/money-map/pages/MoneyMapPage'));
const SettingsPage = lazy(() => import('./features/settings/pages/SettingsPage'));
const InvestmentsPage = lazy(() => import('./features/webull/pages/InvestmentsPage'));
const PlaidOAuthPage = lazy(() => import('./features/linked-accounts/pages/PlaidOAuthPage'));
const BudgetPage = lazy(() => import('./features/budget-plans/pages/BudgetPage'));
const BudgetBuilderPage = lazy(() => import('./features/budget-builder/pages/BudgetBuilderPage'));
const LoginPage = lazy(() => import('./pages/LoginPage'));
const ForgotPasswordPage = lazy(() => import('./pages/ForgotPasswordPage'));

/** Suspends below the shell, so the nav stays put while a page downloads. */
const Page = ({ children }: { children: ReactNode }) => <Suspense fallback={<PageSkeleton />}>{children}</Suspense>;

function App() {
  return (
    <Routes>
      {/* Public auth routes */}
      <Route
        path="/login"
        element={
          <Page>
            <LoginPage />
          </Page>
        }
      />
      <Route
        path="/forgot"
        element={
          <Page>
            <ForgotPasswordPage />
          </Page>
        }
      />

      {/* Protected app routes */}
      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route
            path="/"
            element={
              <Page>
                <MoneyMapPage />
              </Page>
            }
          />
          <Route
            path="/budget"
            element={
              <Page>
                <BudgetPage />
              </Page>
            }
          />
          <Route
            path="/budget/guide"
            element={
              <Page>
                <BudgetBuilderPage />
              </Page>
            }
          />
          <Route
            path="/transactions"
            element={
              <Page>
                <TransactionsPage />
              </Page>
            }
          />
          <Route
            path="/investments"
            element={
              <Page>
                <InvestmentsPage />
              </Page>
            }
          />
          <Route
            path="/settings"
            element={
              <Page>
                <SettingsPage />
              </Page>
            }
          />
          {/* Plaid's OAuth redirect target (Plaid:RedirectUri) when a bank can't open in a popup. */}
          <Route
            path="/plaid-oauth"
            element={
              <Page>
                <PlaidOAuthPage />
              </Page>
            }
          />
        </Route>
      </Route>

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default App;
