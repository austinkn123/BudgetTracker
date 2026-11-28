import { render, screen } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { budgetPlanService } from '../../../shared/services/budgetPlan.service';
import type { BudgetPlan } from '../../../shared/types/api';
import { createTestQueryClient } from '../../../test/renderWithQueryClient';
import FirstBudgetRedirect from './FirstBudgetRedirect';

const renderAfterLink = (plans: BudgetPlan[], otherwise?: string) => {
  vi.spyOn(budgetPlanService, 'getCurrentUserBudgetPlans').mockResolvedValue(plans);
  render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={['/settings/link']}>
        <Routes>
          <Route path="/settings/link" element={<FirstBudgetRedirect otherwise={otherwise} />} />
          <Route path="/budget/guide" element={<p>Budget guide</p>} />
          <Route path="/settings" element={<p>Settings</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
};

describe('FirstBudgetRedirect', () => {
  it('sends someone with no budget to the guide', async () => {
    renderAfterLink([], '/settings');
    expect(await screen.findByText('Budget guide')).toBeInTheDocument();
  });

  it('sends someone who already has a budget to the fallback', async () => {
    renderAfterLink([{ id: 1 } as BudgetPlan], '/settings');
    expect(await screen.findByText('Settings')).toBeInTheDocument();
  });
});
