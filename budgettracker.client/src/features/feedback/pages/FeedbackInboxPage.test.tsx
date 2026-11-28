import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { FeedbackEntry } from '../../../shared/types/api';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { FEEDBACK_PAGE_SIZE } from '../hooks/useFeedback';
import FeedbackInboxPage from './FeedbackInboxPage';

const service = vi.hoisted(() => ({ submit: vi.fn(), getPage: vi.fn() }));
vi.mock('../../../shared/services/feedback.service', () => ({ feedbackService: service }));
const admin = vi.hoisted(() => ({ value: true }));
vi.mock('../../../auth/useIsAdmin', () => ({ useIsAdmin: () => admin.value }));

const entry = (id: number, message = `message ${id}`): FeedbackEntry => ({
  id,
  userId: 3,
  message,
  page: '/budget',
  createdAt: '2026-11-28T01:00:00Z',
});

const renderPage = () => {
  const Wrapper = createQueryWrapper(createTestQueryClient());
  render(
    <MemoryRouter initialEntries={['/admin/feedback']}>
      <Wrapper>
        <Routes>
          <Route path="/admin/feedback" element={<FeedbackInboxPage />} />
          <Route path="/" element={<p>Home</p>} />
        </Routes>
      </Wrapper>
    </MemoryRouter>,
  );
};

describe('FeedbackInboxPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    admin.value = true;
  });

  it('lists feedback newest first with who sent it and from which page', async () => {
    service.getPage.mockResolvedValue([entry(2, 'love the dark mode'), entry(1)]);
    renderPage();

    expect(await screen.findByText('love the dark mode')).toBeInTheDocument();
    expect(screen.getAllByText(/User 3 · \/budget/)).toHaveLength(2);
    expect(service.getPage).toHaveBeenCalledWith(undefined, FEEDBACK_PAGE_SIZE);
  });

  it('says so when there is no feedback', async () => {
    service.getPage.mockResolvedValue([]);
    renderPage();

    expect(await screen.findByText('No feedback yet.')).toBeInTheDocument();
  });

  it('loads the next page from the last id seen', async () => {
    const full = Array.from({ length: FEEDBACK_PAGE_SIZE }, (_, i) => entry(100 - i));
    service.getPage.mockResolvedValueOnce(full).mockResolvedValueOnce([entry(10, 'older one')]);
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'Load more' }));

    expect(await screen.findByText('older one')).toBeInTheDocument();
    expect(service.getPage).toHaveBeenLastCalledWith(100 - FEEDBACK_PAGE_SIZE + 1, FEEDBACK_PAGE_SIZE);
  });

  it('sends anyone who is not an admin home without asking the server', async () => {
    admin.value = false;
    renderPage();

    expect(await screen.findByText('Home')).toBeInTheDocument();
    expect(service.getPage).not.toHaveBeenCalled();
  });
});
