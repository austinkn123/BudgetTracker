import { useEffect } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Input, Modal, ModalActions } from '../../../shared/components/ui';
import { errorMessage } from '../../../shared/utils/errorMessage';
import {
  FEEDBACK_MAX_LENGTH,
  feedbackSchema,
  type FeedbackFormInput,
  type FeedbackFormValues,
} from '../../../shared/validation/feedbackSchema';
import { useSubmitFeedback } from '../hooks/useFeedback';

type FeedbackDialogProps = {
  open: boolean;
  onClose: () => void;
  /** The app path the user is on, sent with the message so it's clear what they were looking at. */
  page: string;
};

/** "Send feedback": a comment or critique on the site, read by the owner in the feedback inbox. */
const FeedbackDialog = ({ open, onClose, page }: FeedbackDialogProps) => {
  const submitFeedback = useSubmitFeedback();
  const { control, handleSubmit, reset } = useForm<FeedbackFormInput, unknown, FeedbackFormValues>({
    resolver: zodResolver(feedbackSchema),
    defaultValues: { message: '' },
  });
  const message = useWatch({ control, name: 'message' }) ?? '';

  useEffect(() => {
    if (open) {
      reset({ message: '' });
      submitFeedback.reset();
    }
    // The mutation object changes identity every render; resetting on open is what matters.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, reset]);

  const submit = handleSubmit(async (values) => {
    await submitFeedback.mutateAsync({ message: values.message, page });
  });

  const sent = submitFeedback.isSuccess;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Send feedback"
      maxWidth="sm"
      disableBackdropClose={submitFeedback.isPending}
      actions={
        sent ? (
          <Button onClick={onClose}>Close</Button>
        ) : (
          <ModalActions
            onCancel={onClose}
            onConfirm={() => void submit().catch(() => undefined)}
            confirmLabel="Send"
            isPending={submitFeedback.isPending}
          />
        )
      }
    >
      {sent ? (
        <Alert severity="success" message="Thanks, your feedback was sent." />
      ) : (
        <div className="space-y-4">
          {submitFeedback.error && (
            <Alert
              severity="error"
              message={errorMessage(submitFeedback.error, "Couldn't send your feedback. Please try again.")}
            />
          )}
          <Input
            control={control}
            name="message"
            label="Your feedback"
            placeholder="What's working, what's confusing, what you'd change…"
            helperText={`${message.length} / ${FEEDBACK_MAX_LENGTH}`}
            maxLength={FEEDBACK_MAX_LENGTH}
            multiline
            rows={6}
            required
            autoFocus
          />
          <p className="text-xs text-ink-muted">Sent with your account and the page you're on, so we can follow up.</p>
        </div>
      )}
    </Modal>
  );
};

export default FeedbackDialog;
