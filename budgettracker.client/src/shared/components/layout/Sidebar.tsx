import { NavLink } from 'react-router-dom';
import { LogOut, User, Wallet } from 'lucide-react';
import { useAuth } from '../../../auth/useAuth';
import { useUser } from '../../../features/user/hooks/useUser';
import { useSignOut } from '../../hooks/useSignOut';
import { Avatar, Button, Separator, Skeleton, Tooltip } from '../ui';
import { cn } from '../../utils/cn';
import { NAV_ITEMS } from './navItems';
import { getAccountLabel, getInitials } from './userIdentity';

export interface SidebarProps {
  /** Called after a nav link is followed, so AppShell can close the mobile sheet. */
  onNavigate?: () => void;
}

const Sidebar = ({ onNavigate }: SidebarProps) => {
  const { data: user, isLoading: loadingUser } = useUser();
  const { user: cognitoUser } = useAuth();
  const { signOut, isSigningOut, error } = useSignOut();

  // useUser() is the reliable source: the Cognito email is empty after a page
  // refresh because getCurrentUser() does not repopulate signInDetails.
  const email = user?.email || cognitoUser?.email || '';
  const accountLabel = getAccountLabel(email, cognitoUser?.username);
  const initials = getInitials(email);

  return (
    <div className="flex h-full flex-col bg-surface">
      {/* Brand */}
      <div className="flex items-center gap-3 px-5 py-5">
        <span className="flex text-primary">
          <Wallet size={24} />
        </span>
        <span className="text-base font-semibold tracking-tight text-ink">BudgetTracker</span>
      </div>

      <Separator />

      {/* Primary navigation */}
      <nav aria-label="Main navigation" className="flex-1 px-3 py-4">
        <ul className="flex flex-col gap-1">
          {NAV_ITEMS.map((item) => (
            <li key={item.path}>
              <NavLink
                to={item.path}
                end={item.end}
                onClick={onNavigate}
                className={({ isActive }) =>
                  cn(
                    'focus-ring flex items-center gap-3 rounded px-3 py-2 text-sm font-medium text-ink-muted transition-colors duration-120',
                    'hover:bg-border-subtle hover:text-ink',
                    isActive && 'bg-primary-subtle font-semibold text-primary hover:bg-primary-subtle hover:text-primary',
                  )
                }
              >
                <item.icon size={18} className="shrink-0" />
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <Separator />

      {/* Account footer */}
      <div className="flex flex-col gap-3 px-4 py-4">
        {loadingUser ? (
          <div className="flex items-center gap-3">
            <Skeleton variant="circular" width={36} height={36} />
            <Skeleton width={130} height={16} />
          </div>
        ) : (
          <div className="flex min-w-0 items-center gap-3">
            <Avatar>{initials || <User size={18} />}</Avatar>
            <Tooltip title={accountLabel}>
              <span className="min-w-0 truncate text-sm text-ink-muted">{accountLabel}</span>
            </Tooltip>
          </div>
        )}

        {error && <span className="block text-xs text-error">{error}</span>}

        <Button
          variant="destructive-ghost"
          size="sm"
          fullWidth
          startIcon={<LogOut size={16} />}
          onClick={signOut}
          loading={isSigningOut}
          className="border-error/30"
        >
          {isSigningOut ? 'Signing out…' : 'Sign out'}
        </Button>
      </div>
    </div>
  );
};

export default Sidebar;
