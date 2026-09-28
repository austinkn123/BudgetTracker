import { format } from 'date-fns';
import { User as UserIcon } from 'lucide-react';
import Card from '../../shared/components/ui/Card';
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

  if (isLoading) return null;

  return (
    <Card
      title={
        <span className="flex items-center gap-2">
          <UserIcon size={18} className="text-primary" />
          User Information
        </span>
      }
    >
      {user ? (
        <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
          <Field label="User ID" value={String(user.id)} />
          <Field label="Email" value={user.email} />
          <Field label="Created" value={format(new Date(user.createdAt), 'PPP')} />
        </div>
      ) : (
        <p className="text-sm italic text-ink-muted">No user data found</p>
      )}
    </Card>
  );
};

export default UserSection;
