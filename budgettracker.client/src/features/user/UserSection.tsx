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
    <section className="bg-white rounded-lg shadow">
      <div className="px-6 py-4 border-b border-gray-200">
        <div className="flex items-center gap-2">
          <UserIcon className="w-5 h-5 text-blue-600" />
          <h2 className="text-xl font-semibold text-gray-900">User Information</h2>
        </div>
      </div>
      <div className="px-6 py-4">
        {user ? (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <span className="text-sm font-medium text-gray-500">User ID:</span>
              <p className="mt-1 text-gray-900">{user.id}</p>
            </div>
            <div>
              <span className="text-sm font-medium text-gray-500">Email:</span>
              <p className="mt-1 text-gray-900">{user.email}</p>
            </div>
            <div>
              <span className="text-sm font-medium text-gray-500">Created At:</span>
              <p className="mt-1 text-gray-900">{format(new Date(user.createdAt), 'PPP')}</p>
            </div>
          </div>
        ) : (
          <p className="text-gray-500 italic">No user data found</p>
        )}
      </div>
    </section>
  );
}
