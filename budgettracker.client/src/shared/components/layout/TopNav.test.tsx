import { render, screen } from '@testing-library/react';
import { TooltipProvider } from '@radix-ui/react-tooltip';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';

import { webullService } from '../../services/webull.service';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { webullView } from '../../../test/webullFixtures';
import TopNav from './TopNav';

vi.mock('../../services/webull.service', () => ({
  webullService: { get: vi.fn(), refresh: vi.fn(), verify: vi.fn(), getHoldings: vi.fn() },
}));
vi.mock('../../../features/user/hooks/useUser', () => ({
  useUser: () => ({ data: undefined, isLoading: false }),
}));
vi.mock('../../../auth/useAuth', () => ({ useAuth: () => ({ user: null }) }));
vi.mock('../../hooks/useSignOut', () => ({
  useSignOut: () => ({ signOut: vi.fn(), isSigningOut: false, error: null }),
}));
vi.mock('../../theme/useTheme', () => ({ useTheme: () => ({ theme: 'light', toggle: vi.fn() }) }));

describe('TopNav', () => {
  it('links to Investments once Webull is connected', async () => {
    vi.mocked(webullService.get).mockResolvedValue(webullView({ state: 'Connected' }));
    const Wrapper = createQueryWrapper(createTestQueryClient());

    render(
      <MemoryRouter>
        <Wrapper>
          <TooltipProvider>
            <TopNav onOpenMobileNav={() => undefined} />
          </TooltipProvider>
        </Wrapper>
      </MemoryRouter>,
    );

    expect(await screen.findByRole('link', { name: 'Investments' })).toHaveAttribute(
      'href',
      '/investments',
    );
  });

  it('reaches Settings from the account controls, not the page links', async () => {
    vi.mocked(webullService.get).mockResolvedValue(webullView({ state: 'Connected' }));
    const Wrapper = createQueryWrapper(createTestQueryClient());

    render(
      <MemoryRouter initialEntries={['/settings']}>
        <Wrapper>
          <TooltipProvider>
            <TopNav onOpenMobileNav={() => undefined} />
          </TooltipProvider>
        </Wrapper>
      </MemoryRouter>,
    );

    const settings = screen.getByRole('link', { name: 'Settings' });
    expect(settings).toHaveAttribute('href', '/settings');
    expect(settings).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('link', { name: 'Budget Plans' })).not.toBeInTheDocument();
  });
});
