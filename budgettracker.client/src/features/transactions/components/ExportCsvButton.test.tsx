import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ExportCsvButton from './ExportCsvButton';

const service = vi.hoisted(() => ({ exportCsv: vi.fn() }));
const download = vi.hoisted(() => vi.fn());
vi.mock('../../../shared/services/transaction.service', () => ({
  transactionService: service,
}));
vi.mock('../../../shared/utils/downloadBlob', () => ({
  downloadBlob: download,
}));

describe('ExportCsvButton (BUD-25)', () => {
  beforeEach(() => vi.clearAllMocks());

  it("exports the list's own filters and saves the server's file name", async () => {
    const user = userEvent.setup();
    const blob = new Blob(['Date,Payee\r\n']);
    service.exportCsv.mockResolvedValue({
      blob,
      fileName: 'budgettracker-transactions-all.csv',
    });
    render(<ExportCsvButton query={{ isPending: false, search: 'uber', plaidAccountId: 'acct-1' }} />);

    await user.click(screen.getByRole('button', { name: 'Export CSV' }));

    await waitFor(() => expect(download).toHaveBeenCalledWith(blob, 'budgettracker-transactions-all.csv'));
    expect(service.exportCsv).toHaveBeenCalledWith({
      isPending: false,
      search: 'uber',
      plaidAccountId: 'acct-1',
    });
  });

  it('says so when the export fails, and saves nothing', async () => {
    const user = userEvent.setup();
    service.exportCsv.mockRejectedValue(new Error('network'));
    render(<ExportCsvButton query={{}} />);

    await user.click(screen.getByRole('button', { name: 'Export CSV' }));

    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't export. Please try again.");
    expect(download).not.toHaveBeenCalled();
  });
});
