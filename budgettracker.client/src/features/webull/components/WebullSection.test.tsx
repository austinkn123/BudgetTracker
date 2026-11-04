import { QueryClient, defaultScheduler, notifyManager } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { webullService } from '../../../shared/services/webull.service';
import type { WebullConnectionView, WebullState } from '../../../shared/types/api';
import { localTime } from '../../../shared/utils/format';
import { deferred } from '../../../test/plaidFixtures';
import { createQueryWrapper, createTestQueryClient } from '../../../test/renderWithQueryClient';
import { webullView } from '../../../test/webullFixtures';
import WebullSection from './WebullSection';

vi.mock('../../../shared/services/webull.service', () => ({
  webullService: { get: vi.fn(), refresh: vi.fn(), verify: vi.fn() },
}));

const getMock = vi.mocked(webullService.get);
const refreshMock = vi.mocked(webullService.refresh);
const verifyMock = vi.mocked(webullService.verify);

const LABELS: Record<WebullState, string> = {
  NotSetUp: 'Not set up',
  NotConnected: 'Not connected',
  WaitingForApproval: 'Waiting for approval',
  Connected: 'Connected',
  ApprovalExpired: 'Approval expired',
  NeedsVerification: 'Needs verification',
};

/** Anything that could take a key, secret or token, including `<input type="password">`. */
const ANY_INPUT = 'input, textarea, select, [contenteditable="true"]';

const KEY_REJECTED_MESSAGE =
  'Webull rejected the app key or secret. Check Webull:AppKey and Webull:AppSecret in appsettings.Local.json.';

const renderSection = (queryClient = createTestQueryClient()) => {
  const Wrapper = createQueryWrapper(queryClient);
  return render(
    <Wrapper>
      <WebullSection />
    </Wrapper>,
  );
};

const renderWith = async (view: WebullConnectionView) => {
  getMock.mockResolvedValue(view);
  const result = renderSection();
  await screen.findByText(LABELS[view.state]);
  return result;
};

describe('WebullSection', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it.each(Object.entries(LABELS) as [WebullState, string][])(
    'shows exactly one state label for %s, and never an input',
    async (state, label) => {
      const { container } = await renderWith(webullView({ state }));

      for (const other of Object.values(LABELS).filter((l) => l !== label)) {
        expect(screen.queryByText(other)).not.toBeInTheDocument();
      }
      expect(screen.getAllByText(label)).toHaveLength(1);
      expect(container.querySelector(ANY_INPUT)).toBeNull();
      expect(screen.queryByRole('button', { name: /disconnect/i })).not.toBeInTheDocument();
    },
  );

  it('explains where the keys go when not set up, with no buttons', async () => {
    await renderWith(webullView({ state: 'NotSetUp', balances: [], asOf: null, canVerify: false }));

    expect(document.body).toHaveTextContent(
      'Webull:AppKey and Webull:AppSecret go in BudgetTracker.Server/appsettings.Local.json',
    );
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('shows "Verify in Webull app" only when the server says it can verify', async () => {
    await renderWith(webullView({ state: 'NeedsVerification', canVerify: false }));
    expect(screen.queryByRole('button', { name: 'Verify in Webull app' })).not.toBeInTheDocument();
  });

  it('verifies once, disabled while pending, and stores the returned view', async () => {
    const user = userEvent.setup();
    const pending = deferred<WebullConnectionView>();
    verifyMock.mockReturnValue(pending.promise);

    await renderWith(webullView({ state: 'NeedsVerification', canVerify: true }));

    const button = screen.getByRole('button', { name: 'Verify in Webull app' });
    await user.dblClick(button);

    expect(button).toBeDisabled();
    await user.click(button);
    expect(verifyMock).toHaveBeenCalledTimes(1);

    pending.resolve(
      webullView({
        state: 'WaitingForApproval',
        approvalExpiresAt: '2026-10-22T18:10:00Z',
        canVerify: false,
      }),
    );

    expect(await screen.findByText('Waiting for approval')).toBeInTheDocument();
    expect(screen.queryByText('Needs verification')).not.toBeInTheDocument();
    // The SMS is on its way; offering it again would only send a second code.
    expect(screen.queryByRole('button', { name: 'Verify in Webull app' })).not.toBeInTheDocument();
  });

  it('never retries verify, since each attempt texts a new SMS code', async () => {
    const user = userEvent.setup();
    verifyMock.mockRejectedValue(new Error('Request failed with status code 502'));
    getMock.mockResolvedValue(webullView({ state: 'NeedsVerification', canVerify: true }));
    // Client default says "retry 3 times"; the verify mutation must override it with retry: false.
    renderSection(
      new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: 3, retryDelay: 0 } },
      }),
    );

    await user.click(await screen.findByRole('button', { name: 'Verify in Webull app' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Couldn't start verification. Please try again.",
    );
    expect(verifyMock).toHaveBeenCalledTimes(1);
  });

  it("shows the server's message when verify is refused", async () => {
    const user = userEvent.setup();
    verifyMock.mockRejectedValue(
      Object.assign(new Error('Request failed with status code 409'), {
        isAxiosError: true,
        response: { status: 409, data: { error: 'Webull is not set up.' } },
      }),
    );

    await renderWith(webullView({ state: 'NotConnected', canVerify: true }));
    await user.click(screen.getByRole('button', { name: 'Verify in Webull app' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Webull is not set up.');
    expect(screen.getByRole('button', { name: 'Verify in Webull app' })).toBeEnabled();
  });

  it('tells the person to approve with the SMS code and when it expires', async () => {
    const expires = '2026-10-22T18:10:00Z';
    await renderWith(webullView({ state: 'WaitingForApproval', approvalExpiresAt: expires }));

    expect(
      screen.getByText(
        `Approve this in the Webull app with the SMS code. Expires at ${localTime(expires)}.`,
      ),
    ).toBeInTheDocument();
  });

  it('says the key was rejected', async () => {
    await renderWith(webullView({ lastError: 'KeyRejected' }));

    expect(screen.getByRole('alert')).toHaveTextContent(KEY_REJECTED_MESSAGE);
  });

  describe('approval polling', () => {
    const NOW = new Date('2026-10-22T18:00:00Z');
    const waiting = (approvalExpiresAt: string) =>
      webullView({ state: 'WaitingForApproval', approvalExpiresAt });

    /** Seeds the cache with `view` so the section starts there; timers are fake from here on. */
    const renderSeeded = (view: WebullConnectionView) => {
      vi.useFakeTimers();
      vi.setSystemTime(NOW);
      const queryClient = createTestQueryClient();
      queryClient.setQueryData(['webull'], view);
      getMock.mockResolvedValue(view);
      renderSection(queryClient);
      return queryClient;
    };

    /**
     * Advance in 1 s steps, each in its own act(), so React commits (and re-runs effects)
     * between polls the way it would in real time. One big act() would hold every render
     * until the end and let a poll fire that a real browser would already have cancelled.
     */
    const advance = async (ms: number) => {
      for (let left = ms; left > 0; left -= 1_000) {
        await act(() => vi.advanceTimersByTimeAsync(Math.min(1_000, left)));
      }
    };

    // React Query passes a cache write to the screen over two chained 0 ms timers. Under fake
    // timers that lands one tick late; notifying synchronously (always inside act here) shows a
    // response at the moment it arrives, as a real browser would within milliseconds.
    beforeEach(() => {
      notifyManager.setScheduler((notify) => notify());
    });

    afterEach(() => {
      notifyManager.setScheduler(defaultScheduler);
      vi.useRealTimers();
    });

    it('polls refresh every 5 s and stores each result', async () => {
      const queryClient = renderSeeded(waiting('2026-10-22T18:05:00Z'));
      const later = { ...waiting('2026-10-22T18:05:00Z'), asOf: '2026-10-22T18:00:05Z' };
      refreshMock.mockResolvedValue(later);

      await advance(4_999);
      expect(refreshMock).not.toHaveBeenCalled();

      await advance(1);
      expect(refreshMock).toHaveBeenCalledTimes(1);
      expect(queryClient.getQueryData(['webull'])).toEqual(later);

      await advance(5_000);
      expect(refreshMock).toHaveBeenCalledTimes(2);
      expect(verifyMock).not.toHaveBeenCalled();
    });

    it('stops polling once the state changes', async () => {
      renderSeeded(waiting('2026-10-22T18:05:00Z'));
      refreshMock
        .mockResolvedValueOnce(waiting('2026-10-22T18:05:00Z'))
        .mockResolvedValue(webullView({ state: 'Connected' }));

      await advance(10_000);
      expect(refreshMock).toHaveBeenCalledTimes(2);

      await advance(60_000);
      expect(refreshMock).toHaveBeenCalledTimes(2);
      expect(screen.getByText('Connected')).toBeInTheDocument();
      expect(verifyMock).not.toHaveBeenCalled();
    });

    it('stops polling 10 s after the approval expires even if the state never changes', async () => {
      // Expires 5 s from now, so polls at 5, 10 and 15 s are inside the cap and 20 s is past it.
      const view = waiting('2026-10-22T18:00:05Z');
      renderSeeded(view);
      refreshMock.mockResolvedValue(view);

      await advance(15_000);
      expect(refreshMock).toHaveBeenCalledTimes(3);

      await advance(120_000);
      expect(refreshMock).toHaveBeenCalledTimes(3);
      expect(verifyMock).not.toHaveBeenCalled();
    });

    it('does not stay on Waiting with no action after the cap', async () => {
      renderSeeded(waiting('2026-10-22T18:00:05Z'));
      // Every poll fails, so only the GET can learn that the server has moved on.
      refreshMock.mockRejectedValue(new Error('Webull is down'));
      getMock.mockResolvedValue(webullView({ state: 'ApprovalExpired', canVerify: true }));
      const getsBeforeCap = getMock.mock.calls.length;

      // Polls at 5, 10 and 15 s are inside the cap (expiry + 10 s); 20 s is past it.
      await advance(15_000);
      expect(screen.getByText('Waiting for approval')).toBeInTheDocument();
      expect(getMock).toHaveBeenCalledTimes(getsBeforeCap);

      await advance(5_000);
      expect(getMock.mock.calls.length).toBeGreaterThan(getsBeforeCap);
      expect(screen.getByText('Approval expired')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Verify in Webull app' })).toBeEnabled();
      expect(refreshMock).toHaveBeenCalledTimes(3);
      expect(verifyMock).not.toHaveBeenCalled();
    });

    it('shows Approval expired with the Verify button when the window passes, and stops polling', async () => {
      renderSeeded(waiting('2026-10-22T18:00:05Z'));
      refreshMock.mockResolvedValue(webullView({ state: 'ApprovalExpired', canVerify: true }));

      await advance(5_000);
      expect(screen.getByText('Approval expired')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Verify in Webull app' })).toBeEnabled();

      await advance(60_000);
      expect(refreshMock).toHaveBeenCalledTimes(1);
      expect(verifyMock).not.toHaveBeenCalled();
    });

    it('never sends verify without a click, even when verify is offered', async () => {
      renderSeeded(webullView({ state: 'ApprovalExpired', canVerify: true }));

      await advance(60_000);
      expect(verifyMock).not.toHaveBeenCalled();
      expect(screen.getByRole('button', { name: 'Verify in Webull app' })).toBeInTheDocument();
    });

    it('does not poll in any other state', async () => {
      renderSeeded(webullView({ state: 'NeedsVerification' }));

      await advance(60_000);
      expect(refreshMock).not.toHaveBeenCalled();
      expect(verifyMock).not.toHaveBeenCalled();
    });
  });
});
