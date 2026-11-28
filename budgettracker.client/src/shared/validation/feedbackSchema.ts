import { z } from 'zod';

/** Mirrors FeedbackLimits.MaxMessageLength on the server. */
export const FEEDBACK_MAX_LENGTH = 2000;

/** Site feedback. Mirrors FeedbackEngine.Validate on the server. */
export const feedbackSchema = z.object({
  message: z
    .string()
    .trim()
    .min(1, "Feedback can't be empty")
    .max(FEEDBACK_MAX_LENGTH, `Feedback must be ${FEEDBACK_MAX_LENGTH} characters or fewer`),
});

export type FeedbackFormInput = z.input<typeof feedbackSchema>;
export type FeedbackFormValues = z.output<typeof feedbackSchema>;
