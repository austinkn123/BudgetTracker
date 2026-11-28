import { useInfiniteQuery, useMutation } from '@tanstack/react-query';
import { feedbackService } from '../../../shared/services/feedback.service';
import type { FeedbackRequest } from '../../../shared/types/api';

export const FEEDBACK_PAGE_SIZE = 50;

export const feedbackKeys = {
  inbox: ['feedback', 'inbox'] as const,
};

/** Send feedback about the site. */
export const useSubmitFeedback = () =>
  useMutation({ mutationFn: (feedback: FeedbackRequest) => feedbackService.submit(feedback) });

/** The admin inbox, newest first, a page at a time. */
export const useFeedbackInbox = (enabled = true) =>
  useInfiniteQuery({
    queryKey: feedbackKeys.inbox,
    queryFn: ({ pageParam }) => feedbackService.getPage(pageParam, FEEDBACK_PAGE_SIZE),
    initialPageParam: undefined as number | undefined,
    getNextPageParam: (lastPage) => (lastPage.length < FEEDBACK_PAGE_SIZE ? undefined : lastPage[lastPage.length - 1]?.id),
    enabled,
  });
