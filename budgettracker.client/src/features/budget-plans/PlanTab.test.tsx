import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PlanTab from './PlanTab';

const reads = vi.hoisted(() => ({ plansFailed: false, refetchPlans: (() => undefined) as () => void }));

vi.mock('./hooks/useBudgetPlans', () => ({
  useBudgetPlans: () =>
    reads.plansFailed
      ? { data: undefined, isLoading: false, isError: true, refetch: reads.refetchPlans }
      : { data: [], isLoading: false, isError: false, refetch: reads.refetchPlans },
}));
vi.mock('../categories/hooks/useCategories', () => ({
  useCategories: () => ({ data: [], isLoading: false, isError: false, refetch: vi.fn() }),
}));
// The sections own their empty states ("No plans yet"); here only whether they render matters.
vi.mock('./BudgetPlansSection', () => ({ default: () => <p>Plans section</p> }));
vi.mock('../categories/CategoriesSection', () => ({ default: () => <p>Categories section</p> }));

const renderTab = () => render(<PlanTab />, { wrapper: MemoryRouter });

describe('PlanTab', () => {
  beforeEach(() => {
    reads.plansFailed = false;
    reads.refetchPlans = vi.fn();
  });

  it('shows the plans and categories when both load', () => {
    renderTab();

    expect(screen.getByText('Plans section')).toBeInTheDocument();
    expect(screen.getByText('Categories section')).toBeInTheDocument();
  });

  it('shows a load error instead of empty sections when the plans read fails', async () => {
    reads.plansFailed = true;
    renderTab();

    expect(screen.getByText("Couldn't load your plans and categories")).toBeInTheDocument();
    expect(screen.queryByText('Plans section')).not.toBeInTheDocument();
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }));
    expect(reads.refetchPlans).toHaveBeenCalled();
  });
});
