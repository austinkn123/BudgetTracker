import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';

import App from './App';

// Auth and the shell are covered elsewhere; here they only need to let the route through.
vi.mock('./auth/ProtectedRoute', async () => {
  const { Outlet } = await import('react-router-dom');
  return { default: () => <Outlet /> };
});
vi.mock('./shared/components/layout/AppShell', async () => {
  const { Outlet } = await import('react-router-dom');
  return { default: () => <Outlet /> };
});
vi.mock('./features/webull/pages/InvestmentsPage', () => ({
  default: () => <p>Investments page</p>,
}));

describe('App routes', () => {
  it('renders the Investments page at /investments', () => {
    render(
      <MemoryRouter initialEntries={['/investments']}>
        <App />
      </MemoryRouter>,
    );

    expect(screen.getByText('Investments page')).toBeInTheDocument();
  });
});
