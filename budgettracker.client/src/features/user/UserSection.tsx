import { format } from 'date-fns';
import { User as UserIcon } from 'lucide-react';
import { useUser } from './hooks/useUser';

type UserSectionProps = {
  isLoading: boolean;
};

export default function UserSection({ isLoading }: UserSectionProps) {
  const { data: user } = useUser();

  if (isLoading) return null;

  return (
    <section className="bg-surface rounded-lg shadow">
      <div className="px-6 py-4 border-b border-border">
        <div className="flex items-center gap-2">
          <UserIcon className="w-5 h-5 text-primary" />
          <h2 className="text-xl font-semibold text-ink">User Information</h2>
        </div>
      </div>
      <div className="px-6 py-4">
        {user ? (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <span className="text-sm font-medium text-ink-muted">User ID:</span>
              <p className="mt-1 text-ink">{user.id}</p>
            </div>
            <div>
              <span className="text-sm font-medium text-ink-muted">Email:</span>
              <p className="mt-1 text-ink">{user.email}</p>
            </div>
            <div>
              <span className="text-sm font-medium text-ink-muted">Created At:</span>
              <p className="mt-1 text-ink">{format(new Date(user.createdAt), 'PPP')}</p>
            </div>
          </div>
        ) : (
          <p className="text-ink-muted italic">No user data found</p>
        )}
      </div>
    </section>
  );
}
