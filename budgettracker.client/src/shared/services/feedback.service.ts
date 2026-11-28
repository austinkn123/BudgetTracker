import api from '../api';
import type { FeedbackEntry, FeedbackRequest } from '../types/api';

/** `/api/feedback`: any signed-in user sends; only the Cognito "admin" group reads. */
export const feedbackService = {
  submit: async (feedback: FeedbackRequest): Promise<number> => {
    const response = await api.post<{ id: number }>('/feedback', feedback);
    return response.data.id;
  },

  /** Newest first. Pass the last id seen as `before` for the next page. */
  getPage: async (before?: number, take?: number): Promise<FeedbackEntry[]> => {
    const response = await api.get<FeedbackEntry[]>('/feedback', { params: { before, take } });
    return response.data;
  },
};
