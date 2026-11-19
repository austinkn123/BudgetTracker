import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { plaidService } from '../../../shared/services/plaid.service';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import TransactionsPage from './TransactionsPage';

vi.mock('../TransactionsSection', () => ({ default: () => <p>ledger</p> }));
vi.mock('../hooks/useTransactionSummary', () => ({
  useTransactionSummary: () => ({ data: undefined, isLoading: false }),
}));

describe('TransactionsPage', () => {
  it('pulls stale banks once when opened, so a direct visit never shows day-old data (BUD-6)', async () => {
    const sync = vi
      .spyOn(plaidService, 'sync')
      .mockResolvedValue({ inserted: 0, updated: 0, removed: 0, syncedAt: '2026-11-19T00:00:00Z' });

    render(<TransactionsPage />, { wrapper: createQueryWrapper(createTestQueryClient()) });

    expect(await screen.findByText('ledger')).toBeInTheDocument();
    await waitFor(() => expect(sync).toHaveBeenCalledTimes(1));
    expect(sync).toHaveBeenCalledWith(6);
  });
});
