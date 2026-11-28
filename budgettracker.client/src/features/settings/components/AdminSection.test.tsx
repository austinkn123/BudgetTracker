import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import AdminSection from './AdminSection';

const admin = vi.hoisted(() => ({ value: false }));
vi.mock('../../../auth/useIsAdmin', () => ({ useIsAdmin: () => admin.value }));

const renderSection = () =>
  render(
    <MemoryRouter>
      <AdminSection />
    </MemoryRouter>,
  );

describe('AdminSection', () => {
  it('links admins to the feedback inbox', () => {
    admin.value = true;
    renderSection();

    expect(screen.getByRole('link', { name: 'Read feedback from users' })).toHaveAttribute('href', '/admin/feedback');
  });

  it('renders nothing for everyone else', () => {
    admin.value = false;
    const { container } = renderSection();

    expect(container).toBeEmptyDOMElement();
  });
});
