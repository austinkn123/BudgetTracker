import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { Category } from '../../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import CategoryRuleDialog from './CategoryRuleDialog';

const service = vi.hoisted(() => ({
  getAll: vi.fn(),
  preview: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  delete: vi.fn(),
}));
vi.mock('../../../shared/services/categoryRule.service', () => ({ categoryRuleService: service }));

const CATEGORIES: Category[] = [
  { id: 7, userId: 1, name: 'Rideshare', categoryType: 'Expense' },
  { id: 9, userId: 1, name: 'Dining out', categoryType: 'Expense' },
];

const renderDialog = (props: Partial<Parameters<typeof CategoryRuleDialog>[0]> = {}) => {
  const onClose = vi.fn();
  const onSaved = vi.fn();
  const Wrapper = createQueryWrapper(createTestQueryClient());
  render(
    <Wrapper>
      <CategoryRuleDialog
        open
        initialValues={{ pattern: 'UBER 072515 SF', matchType: 'Contains', categoryId: 7, applyToExisting: false }}
        categories={CATEGORIES}
        onClose={onClose}
        onSaved={onSaved}
        {...props}
      />
    </Wrapper>,
  );
  return { onClose, onSaved, dialog: screen.getByRole('dialog') };
};

describe('CategoryRuleDialog (BUD-24)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    service.preview.mockResolvedValue(3);
    service.create.mockResolvedValue({ id: 11, appliedCount: 3 });
    service.update.mockResolvedValue(undefined);
  });

  it('prefills from the row and shows how many existing rows the rule would file', async () => {
    const { dialog } = renderDialog();

    expect(within(dialog).getByRole('textbox', { name: /Payee text/ })).toHaveValue('UBER 072515 SF');
    expect(await within(dialog).findByLabelText('Also apply to 3 existing uncategorized transactions')).toBeInTheDocument();
    expect(service.preview).toHaveBeenLastCalledWith({ pattern: 'UBER 072515 SF', matchType: 'Contains', categoryId: 7 });
  });

  it('asks again with the edited pattern once typing settles', async () => {
    const user = userEvent.setup();
    const { dialog } = renderDialog();
    const pattern = within(dialog).getByRole('textbox', { name: /Payee text/ });

    await user.clear(pattern);
    await user.type(pattern, 'uber');

    await waitFor(() =>
      expect(service.preview).toHaveBeenLastCalledWith({ pattern: 'uber', matchType: 'Contains', categoryId: 7 }),
    );
  });

  it('creates the rule with apply-to-existing and reports how many rows it filed', async () => {
    const user = userEvent.setup();
    const { dialog, onSaved, onClose } = renderDialog();

    await user.click(await within(dialog).findByLabelText('Also apply to 3 existing uncategorized transactions'));
    await user.click(within(dialog).getByRole('button', { name: 'Create rule' }));

    await waitFor(() =>
      expect(service.create).toHaveBeenCalledWith({
        pattern: 'UBER 072515 SF',
        matchType: 'Contains',
        categoryId: 7,
        applyToExisting: true,
      }),
    );
    expect(onSaved).toHaveBeenCalledWith('Rule saved. Filed 3 transactions.');
    expect(onClose).toHaveBeenCalled();
  });

  it('shows the field error and saves nothing when the pattern is blank', async () => {
    const user = userEvent.setup();
    const { dialog } = renderDialog();

    await user.clear(within(dialog).getByRole('textbox', { name: /Payee text/ }));
    await user.click(within(dialog).getByRole('button', { name: 'Create rule' }));

    expect(await within(dialog).findByText('Pattern is required')).toBeInTheDocument();
    expect(service.create).not.toHaveBeenCalled();
  });

  it("shows the server's message and stays open when saving fails", async () => {
    const user = userEvent.setup();
    service.create.mockRejectedValue({ response: { data: { error: 'A rule for "UBER 072515 SF" already exists' } } });
    const { dialog, onClose } = renderDialog();

    await user.click(within(dialog).getByRole('button', { name: 'Create rule' }));

    expect(await within(dialog).findByText('A rule for "UBER 072515 SF" already exists')).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('edits an existing rule without offering to apply it or previewing', async () => {
    const user = userEvent.setup();
    const { dialog, onSaved } = renderDialog({ ruleId: 4 });

    expect(within(dialog).queryByLabelText(/Also apply/)).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }));

    await waitFor(() =>
      expect(service.update).toHaveBeenCalledWith(4, { pattern: 'UBER 072515 SF', matchType: 'Contains', categoryId: 7 }),
    );
    expect(onSaved).toHaveBeenCalledWith('Rule saved.');
    expect(service.preview).not.toHaveBeenCalled();
  });
});
