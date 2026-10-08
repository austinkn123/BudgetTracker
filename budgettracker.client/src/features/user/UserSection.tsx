import { format } from 'date-fns';
import { User as UserIcon } from 'lucide-react';
import { Card, Skeleton } from '../../shared/components/ui';
import { useUser } from './hooks/useUser';

type UserSectionProps = {
  isLoading: boolean;
};

const Field = ({ label, value }: { label: string; value: string }) => (
  <div>
    <span className="text-2xs font-semibold uppercase tracking-[0.06em] text-ink-muted">
      {label}
    </span>
    <p className="mt-1 text-sm text-ink">{value}</p>
  </div>
);

const UserSection = ({ isLoading }: UserSectionProps) => {
  const { data: user } = useUser();

  const title = (
    <span className="flex items-center gap-2">
      <UserIcon size={18} className="text-primary" />
      Account
    </span>
  );

  if (isLoading) {
    return (
      <Card title={title}>
        <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
          {[0, 1].map((i) => (
            <div key={i} className="space-y-2">
              <Skeleton width={64} height={10} />
              <Skeleton width={180} height={14} />
            </div>
          ))}
        </div>
      </Card>
    );
  }

  return (
    <Card title={title}>
      {user ? (
        <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
          <Field label="Email" value={user.email} />
          <Field label="Member since" value={format(new Date(user.createdAt), 'PPP')} />
        </div>
      ) : (
        <p className="text-sm text-ink-muted">
          We couldn't load your account details. Refresh the page to try again.
        </p>
      )}
    </Card>
  );
};

export default UserSection;
