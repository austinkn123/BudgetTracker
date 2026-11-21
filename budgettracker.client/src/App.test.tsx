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
  it('loads every page on demand rather than in the main bundle (BUD-28)', async () => {
    const source = (await import('./App.tsx?raw')).default;

    expect(source).not.toMatch(/^import \w+ from '\.\/(features|pages)\//m);
  });


  it('renders the Investments page at /investments', async () => {
    render(
      <MemoryRouter initialEntries={['/investments']}>
        <App />
      </MemoryRouter>,
    );

    // Pages load on demand (BUD-28), so the route renders once its chunk resolves.
    expect(await screen.findByText('Investments page')).toBeInTheDocument();
  });
});
