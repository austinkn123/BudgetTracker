import { TooltipProvider } from '@radix-ui/react-tooltip';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { Category, CategoryUsage } from '../../shared/types/api';
import CategoriesSection from './CategoriesSection';

const GROCERIES: Category = {
  id: 7,
  userId: 1,
  name: 'Groceries',
  categoryType: 'Expense',
  plaidCategoryPrimary: 'FOOD_AND_DRINK',
};
// Usage puts Groceries in the ranked list (rather than "Never used"), where the row and its
// Delete button live.
const USAGE: CategoryUsage[] = [
  { categoryId: 7, transactionCount: 3, inflowSince: 0, outflowSince: -120 },
];

const mutations = vi.hoisted(() => ({
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
}));

vi.mock('./hooks/useCategories', () => ({
  useCategories: () => ({ data: [GROCERIES], isLoading: false, error: null }),
}));
vi.mock('./hooks/useCategoryUsage', () => ({
  useCategoryUsage: () => ({ data: USAGE }),
}));
vi.mock('../budget-plans/hooks/useBudgetPlans', () => ({
  useBudgetPlans: () => ({ data: [], isLoading: false, error: null }),
}));
vi.mock('./hooks/useCategoryMutations', () => ({
  useCategoryMutations: () => ({
    createCategory: { mutateAsync: mutations.create, isPending: false },
    updateCategory: { mutateAsync: mutations.update, isPending: false },
    deleteCategory: { mutateAsync: mutations.remove, isPending: false },
  }),
}));

// Radix Select relies on pointer-capture and scrollIntoView, which jsdom does not implement.
beforeAll(() => {
  Element.prototype.hasPointerCapture ??= () => false;
  Element.prototype.releasePointerCapture ??= () => undefined;
  Element.prototype.scrollIntoView ??= () => undefined;
});

beforeEach(() => {
  mutations.create.mockReset().mockResolvedValue(undefined);
  mutations.update.mockReset().mockResolvedValue(undefined);
  mutations.remove.mockReset().mockResolvedValue(undefined);
});

const renderSection = () => {
  const user = userEvent.setup();
  render(
    <CategoriesSection isLoading={false} setStatusMessage={vi.fn()} setStatusError={vi.fn()} />,
    { wrapper: TooltipProvider },
  );
  return user;
};

describe('CategoriesSection', () => {
  it('sends an explicit empty mapping when "No automatic mapping" is chosen', async () => {
    const user = renderSection();

    await user.click(screen.getByRole('button', { name: /^Groceries/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Edit category' });
    const mapping = within(dialog).getByRole('combobox', { name: 'Auto-categorize imports as' });
    expect(mapping).toHaveTextContent('Food and drink');

    await user.click(mapping);
    await user.click(await screen.findByRole('option', { name: 'No automatic mapping' }));
    await user.click(within(dialog).getByRole('button', { name: 'Save' }));

    // '' (clear it), never null/undefined, which the server reads as "not supplied" and preserves.
    await waitFor(() =>
      expect(mutations.update).toHaveBeenCalledExactlyOnceWith({
        id: 7,
        userId: 0,
        name: 'Groceries',
        categoryType: 'Expense',
        plaidCategoryPrimary: '',
      }),
    );
  });

  it('asks for confirmation before deleting a category', async () => {
    const user = renderSection();

    await user.click(screen.getByRole('button', { name: 'Delete Groceries' }));

    const confirm = await screen.findByRole('dialog', { name: 'Delete category?' });
    expect(confirm).toHaveTextContent('Delete Groceries? It is used in 3 transactions');
    expect(mutations.remove).not.toHaveBeenCalled();
  });
});
