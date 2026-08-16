import { NavLink } from 'react-router-dom';
import { LogOut, User, Wallet } from 'lucide-react';
import { useAuth } from '../../../auth/useAuth';
import { useUser } from '../../../features/user/hooks/useUser';
import { useSignOut } from '../../hooks/useSignOut';
import { Skeleton, Tooltip } from '../ui';
import { cn } from '../../utils/cn';
import { NAV_ITEMS } from './navItems';
import { getAccountLabel, getInitials } from './userIdentity';

export interface SidebarProps {
  /** Called after a nav link is followed, so AppShell can close the mobile sheet. */
  onNavigate?: () => void;
}

/**
 * Dark navigation rail (BUD-20). Deliberately inverted against the light
 * content area — it anchors the layout and matches the dashboard hero.
 */
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
    <div className="flex h-full flex-col bg-grey-900">
      {/* Brand */}
      <div className="flex items-center gap-2.5 px-5 pb-6 pt-6">
        <span className="flex text-primary-light">
          <Wallet size={22} />
        </span>
        <span className="text-[15px] font-semibold tracking-[-0.01em] text-white">
          BudgetTracker
        </span>
      </div>

      {/* Primary navigation */}
      <nav aria-label="Main navigation" className="flex-1 px-3">
        <p className="mb-2 px-3 text-2xs font-semibold uppercase tracking-[0.08em] text-white/35">
          Menu
        </p>
        <ul className="flex flex-col gap-0.5">
          {NAV_ITEMS.map((item) => (
            <li key={item.path}>
              <NavLink
                to={item.path}
                end={item.end}
                onClick={onNavigate}
                className={({ isActive }) =>
                  cn(
                    'group relative flex items-center gap-3 rounded-lg px-3 py-2 text-[13.5px] font-medium transition-colors duration-120',
                    'outline-none focus-visible:ring-2 focus-visible:ring-primary-light/60',
                    isActive
                      ? 'bg-white/[0.09] text-white'
                      : 'text-white/55 hover:bg-white/[0.05] hover:text-white/90',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    {/* Active rail marker */}
                    <span
                      aria-hidden
                      className={cn(
                        'absolute left-0 h-5 w-0.5 rounded-r-full bg-primary-light transition-opacity duration-120',
                        isActive ? 'opacity-100' : 'opacity-0',
                      )}
                    />
                    <item.icon
                      size={17}
                      className={cn('shrink-0', isActive ? 'text-primary-light' : 'text-white/45')}
                    />
                    {item.label}
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      {/* Account footer */}
      <div className="border-t border-white/[0.08] p-3">
        {loadingUser ? (
          <div className="flex items-center gap-3 px-2 py-1.5">
            <Skeleton variant="circular" width={32} height={32} className="bg-white/10" />
            <Skeleton width={120} height={12} className="bg-white/10" />
          </div>
        ) : (
          <div className="flex min-w-0 items-center gap-3 px-2 py-1.5">
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/25 text-[11px] font-semibold text-primary-light">
              {initials || <User size={15} />}
            </span>
            <Tooltip title={accountLabel}>
              <span className="min-w-0 truncate text-[13px] text-white/60">{accountLabel}</span>
            </Tooltip>
          </div>
        )}

        {error && <p className="px-2 pb-1 text-xs text-error-light">{error}</p>}

        <button
          type="button"
          onClick={signOut}
          disabled={isSigningOut}
          className="mt-1 flex w-full items-center gap-3 rounded-lg px-3 py-2 text-[13.5px] font-medium text-white/55 outline-none transition-colors duration-120 hover:bg-white/[0.05] hover:text-white/90 focus-visible:ring-2 focus-visible:ring-primary-light/60 disabled:opacity-50"
        >
          <LogOut size={17} className="shrink-0 text-white/45" />
          {isSigningOut ? 'Signing out…' : 'Sign out'}
        </button>
      </div>
    </div>
  );
};

export default Sidebar;
