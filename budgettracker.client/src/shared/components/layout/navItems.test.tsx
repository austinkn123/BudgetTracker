import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { webullService } from '../../services/webull.service';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { webullView } from '../../../test/webullFixtures';
import { useNavItems } from './navItems';

vi.mock('../../services/webull.service', () => ({
  webullService: { get: vi.fn(), refresh: vi.fn(), verify: vi.fn(), getHoldings: vi.fn() },
}));

const getMock = vi.mocked(webullService.get);

const renderNav = () =>
  renderHook(() => useNavItems().map((item) => item.label), {
    wrapper: createQueryWrapper(createTestQueryClient()),
  });

describe('useNavItems', () => {
  beforeEach(() => {
    getMock.mockReset();
  });

  it('hides Investments while the Webull state is unknown, so it never flashes', () => {
    getMock.mockReturnValue(new Promise(() => {}));

    const { result } = renderNav();

    expect(result.current).not.toContain('Investments');
    expect(result.current).toContain('Dashboard');
  });

  it('keeps Investments hidden when Webull is not set up', async () => {
    getMock.mockResolvedValue(webullView({ state: 'NotSetUp', balances: [] }));

    const { result } = renderNav();

    await waitFor(() => expect(getMock).toHaveBeenCalled());
    await waitFor(() => expect(result.current).toHaveLength(5));
    expect(result.current).not.toContain('Investments');
  });

  it('keeps Investments hidden when the Webull state could not be read', async () => {
    getMock.mockRejectedValue(new Error('500'));

    const { result } = renderNav();

    await waitFor(() => expect(getMock).toHaveBeenCalled());
    expect(result.current).not.toContain('Investments');
  });

  it('shows Investments, last, once Webull is set up', async () => {
    getMock.mockResolvedValue(webullView({ state: 'Connected' }));

    const { result } = renderNav();

    await waitFor(() => expect(result.current).toContain('Investments'));
    expect(result.current).toEqual(['Money Map', 'Budget', 'Dashboard', 'Cash Flow', 'Transactions', 'Investments']);
  });
});
