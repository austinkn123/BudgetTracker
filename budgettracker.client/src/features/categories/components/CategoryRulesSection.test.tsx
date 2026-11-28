import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { CategoryRule } from '../../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import CategoryRulesSection from './CategoryRulesSection';

const rules = vi.hoisted(() => ({ getAll: vi.fn(), preview: vi.fn(), create: vi.fn(), update: vi.fn(), delete: vi.fn() }));
const categories = vi.hoisted(() => ({ getCurrentUserCategories: vi.fn() }));
vi.mock('../../../shared/services/categoryRule.service', () => ({ categoryRuleService: rules }));
vi.mock('../../../shared/services/category.service', () => ({ categoryService: categories }));

const UBER: CategoryRule = { id: 4, pattern: 'uber', matchType: 'Contains', categoryId: 7, categoryName: 'Rideshare', matchedCount: 12 };
const STARBUCKS: CategoryRule = { id: 5, pattern: 'Starbucks', matchType: 'Exact', categoryId: 9, categoryName: 'Coffee', matchedCount: 1 };

const renderSection = () => {
  const Wrapper = createQueryWrapper(createTestQueryClient());
  render(
    <Wrapper>
      <CategoryRulesSection />
    </Wrapper>,
  );
};

describe('CategoryRulesSection (BUD-24)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    rules.getAll.mockResolvedValue([UBER, STARBUCKS]);
    rules.delete.mockResolvedValue(undefined);
    rules.update.mockResolvedValue(undefined);
    categories.getCurrentUserCategories.mockResolvedValue([
      { id: 7, userId: 1, name: 'Rideshare', categoryType: 'Expense' },
      { id: 9, userId: 1, name: 'Coffee', categoryType: 'Expense' },
    ]);
  });

  it('lists each rule with its match, category and filed count', async () => {
    renderSection();

    expect(await screen.findByText(/Payee contains “uber”/)).toHaveTextContent('Payee contains “uber” → Rideshare');
    expect(screen.getByText('12 transactions filed')).toBeInTheDocument();
    expect(screen.getByText(/Payee is exactly “Starbucks”/)).toHaveTextContent('Payee is exactly “Starbucks” → Coffee');
    expect(screen.getByText('1 transaction filed')).toBeInTheDocument();
  });

  it('explains where rules come from when there are none', async () => {
    rules.getAll.mockResolvedValue([]);
    renderSection();

    expect(await screen.findByText(/No rules yet/)).toBeInTheDocument();
  });

  it('edits a rule in the dialog, prefilled', async () => {
    const user = userEvent.setup();
    renderSection();

    await user.click(await screen.findByRole('button', { name: 'Edit rule uber' }));
    const dialog = await screen.findByRole('dialog', { name: 'Edit rule' });
    const pattern = within(dialog).getByRole('textbox', { name: /Payee text/ });
    expect(pattern).toHaveValue('uber');

    await user.clear(pattern);
    await user.type(pattern, 'uber trip');
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(rules.update).toHaveBeenCalledWith(4, { pattern: 'uber trip', matchType: 'Contains', categoryId: 7 }));
    expect(await screen.findByText('Rule saved.')).toBeInTheDocument();
  });

  it('deletes a rule only after confirming', async () => {
    const user = userEvent.setup();
    renderSection();

    await user.click(await screen.findByRole('button', { name: 'Delete rule Starbucks' }));
    const confirm = await screen.findByRole('dialog', { name: 'Delete rule?' });
    expect(rules.delete).not.toHaveBeenCalled();
    await user.click(within(confirm).getByRole('button', { name: 'Delete rule' }));

    await waitFor(() => expect(rules.delete).toHaveBeenCalledWith(5));
    expect(await screen.findByText('Rule deleted.')).toBeInTheDocument();
  });
});

describe('CategoryRulesSection when the rules fail to load', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    categories.getCurrentUserCategories.mockResolvedValue([]);
  });

  it('says so with Try again, which loads them', async () => {
    rules.getAll.mockRejectedValue(new Error('500'));
    renderSection();

    // The hook retries once before giving up.
    const alert = await screen.findByRole('alert', {}, { timeout: 4000 });
    expect(alert).toHaveTextContent("Couldn't load your rules");
    expect(screen.queryByText(/No rules yet/)).not.toBeInTheDocument();

    rules.getAll.mockResolvedValue([UBER]);
    await userEvent.click(within(alert).getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText(/Payee contains “uber”/)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
