import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import FeedbackDialog from './FeedbackDialog';

const service = vi.hoisted(() => ({ submit: vi.fn(), getPage: vi.fn() }));
vi.mock('../../../shared/services/feedback.service', () => ({ feedbackService: service }));

const renderDialog = () => {
  const onClose = vi.fn();
  const Wrapper = createQueryWrapper(createTestQueryClient());
  render(
    <Wrapper>
      <FeedbackDialog open onClose={onClose} page="/budget" />
    </Wrapper>,
  );
  return { onClose, dialog: screen.getByRole('dialog') };
};

describe('FeedbackDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    service.submit.mockResolvedValue(1);
  });

  it('sends the trimmed message with the current page, then thanks the user', async () => {
    const user = userEvent.setup();
    const { dialog } = renderDialog();

    await user.type(within(dialog).getByRole('textbox', { name: /Your feedback/ }), '  the chart is great  ');
    await user.click(within(dialog).getByRole('button', { name: 'Send' }));

    expect(await within(dialog).findByText('Thanks, your feedback was sent.')).toBeInTheDocument();
    expect(service.submit).toHaveBeenCalledWith({ message: 'the chart is great', page: '/budget' });
  });

  it('shows the error at the field and sends nothing when the message is blank', async () => {
    const user = userEvent.setup();
    const { dialog } = renderDialog();

    await user.type(within(dialog).getByRole('textbox', { name: /Your feedback/ }), '   ');
    await user.click(within(dialog).getByRole('button', { name: 'Send' }));

    expect(await within(dialog).findByText("Feedback can't be empty")).toBeInTheDocument();
    expect(service.submit).not.toHaveBeenCalled();
  });

  it("shows the server's message, such as the rate limit", async () => {
    service.submit.mockRejectedValue({ response: { status: 429, data: { error: "You've sent a lot of feedback recently." } } });
    const user = userEvent.setup();
    const { dialog } = renderDialog();

    await user.type(within(dialog).getByRole('textbox', { name: /Your feedback/ }), 'again');
    await user.click(within(dialog).getByRole('button', { name: 'Send' }));

    expect(await within(dialog).findByText("You've sent a lot of feedback recently.")).toBeInTheDocument();
  });

  it('counts characters against the limit', async () => {
    const user = userEvent.setup();
    const { dialog } = renderDialog();

    await user.type(within(dialog).getByRole('textbox', { name: /Your feedback/ }), 'abc');

    expect(within(dialog).getByText('3 / 2000')).toBeInTheDocument();
  });
});
