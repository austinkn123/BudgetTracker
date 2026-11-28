import { Link } from 'react-router-dom';
import { useIsAdmin } from '../../../auth/useIsAdmin';
import { Card } from '../../../shared/components/ui';

/** Admin tools, shown only to the Cognito "admin" group. Renders nothing for everyone else. */
const AdminSection = () => {
  const isAdmin = useIsAdmin();
  if (!isAdmin) return null;

  return (
    <Card title="Admin" subtitle="Only you see this section." contentClassName="text-sm">
      <Link to="/admin/feedback" className="focus-ring rounded-sm font-medium text-primary hover:underline dark:text-primary-light">
        Read feedback from users
      </Link>
    </Card>
  );
};

export default AdminSection;
