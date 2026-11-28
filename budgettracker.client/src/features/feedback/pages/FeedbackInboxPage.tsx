import { Navigate } from 'react-router-dom';
import { useIsAdmin } from '../../../auth/useIsAdmin';
import { Alert, Button, Card, PageHeader, Skeleton } from '../../../shared/components/ui';
import { localDateTime } from '../../../shared/utils/format';
import { useFeedbackInbox } from '../hooks/useFeedback';

/** Admin-only: every user's feedback, newest first. Anyone else is sent home (the server returns 403 to them too). */
const FeedbackInboxPage = () => {
  const isAdmin = useIsAdmin();
  const inbox = useFeedbackInbox(isAdmin);

  if (!isAdmin) return <Navigate to="/" replace />;

  const entries = inbox.data?.pages.flat() ?? [];

  return (
    <div className="stagger-in space-y-6">
      <PageHeader title="Feedback" description="What people have sent from the Send feedback button, newest first." />

      {inbox.isError && <Alert severity="error" message="Couldn't load feedback. Please try again." />}

      {inbox.isPending ? (
        <div className="space-y-3" aria-label="Loading feedback">
          <Skeleton height={88} />
          <Skeleton height={88} />
        </div>
      ) : entries.length === 0 && !inbox.isError ? (
        <Card>
          <p className="text-sm text-ink-muted">No feedback yet.</p>
        </Card>
      ) : (
        <ul className="space-y-3">
          {entries.map((entry) => (
            <li key={entry.id}>
              <Card padding="sm">
                <p className="whitespace-pre-wrap break-words text-sm text-ink">{entry.message}</p>
                <p className="mt-2 text-xs text-ink-muted">
                  {localDateTime(entry.createdAt)} · User {entry.userId}
                  {entry.page && <> · {entry.page}</>}
                </p>
              </Card>
            </li>
          ))}
        </ul>
      )}

      {inbox.hasNextPage && (
        <Button variant="ghost" onClick={() => void inbox.fetchNextPage()} loading={inbox.isFetchingNextPage}>
          Load more
        </Button>
      )}
    </div>
  );
};

export default FeedbackInboxPage;
