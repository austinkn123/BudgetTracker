import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { LogOut, PanelLeftClose, PanelLeftOpen, User, Wallet } from 'lucide-react';
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
  /** Icon-rail mode. Ignored on the mobile sheet. */
  collapsed?: boolean;
  /** Shows the pin control and reports explicit expand/collapse intent. */
  onTogglePin?: () => void;
  pinned?: boolean;
}

/** Wraps children in a tooltip only while the rail is collapsed. */
const RailTip = ({
  collapsed,
  label,
  children,
}: {
  collapsed: boolean;
  label: string;
  children: ReactNode;
}) => (collapsed ? <Tooltip title={label}>{children}</Tooltip> : <>{children}</>);

/**
 * Dark navigation rail (BUD-14, BUD-20).
 *
 * Collapses to an icon rail — driven by scroll direction in AppShell, or
 * pinned open/closed by the user. Labels stay in the DOM and are hidden with
 * `sr-only` so the accessible name survives collapse.
 */
const Sidebar = ({ onNavigate, collapsed = false, onTogglePin, pinned }: SidebarProps) => {
  const { data: user, isLoading: loadingUser } = useUser();
  const { user: cognitoUser } = useAuth();
  const { signOut, isSigningOut, error } = useSignOut();

  // useUser() is the reliable source: the Cognito email is empty after a page
  // refresh because getCurrentUser() does not repopulate signInDetails.
  const email = user?.email || cognitoUser?.email || '';
  const accountLabel = getAccountLabel(email, cognitoUser?.username);
  const initials = getInitials(email);

  return (
    <div className="flex h-full flex-col overflow-hidden bg-grey-900">
      {/* Brand */}
      <div
        className={cn(
          'flex items-center gap-2.5 pb-6 pt-6 transition-[padding] duration-240 ease-out-soft motion-reduce:transition-none',
          collapsed ? 'justify-center px-0' : 'px-5',
        )}
      >
        <span className="flex shrink-0 text-primary-light">
          <Wallet size={22} />
        </span>
        <span
          className={cn(
            'whitespace-nowrap text-[15px] font-semibold tracking-[-0.01em] text-white transition-[opacity,transform] duration-160 ease-out-soft motion-reduce:transition-none',
            collapsed && 'pointer-events-none w-0 -translate-x-1 opacity-0',
          )}
        >
          BudgetTracker
        </span>
      </div>

      {/* Primary navigation */}
      <nav aria-label="Main navigation" className="flex-1 px-3">
        <p
          className={cn(
            'mb-2 px-3 text-2xs font-semibold uppercase tracking-[0.08em] text-white/35 transition-opacity duration-160 motion-reduce:transition-none',
            collapsed && 'opacity-0',
          )}
          aria-hidden={collapsed}
        >
          Menu
        </p>
        <ul className="flex flex-col gap-0.5">
          {NAV_ITEMS.map((item) => (
            <li key={item.path}>
              <RailTip collapsed={collapsed} label={item.label}>
                <NavLink
                  to={item.path}
                  end={item.end}
                  onClick={onNavigate}
                  className={({ isActive }) =>
                    cn(
                      'group relative flex items-center rounded-lg py-2 text-[13.5px] font-medium transition-colors duration-120',
                      'outline-none focus-visible:ring-2 focus-visible:ring-primary-light/60',
                      collapsed ? 'justify-center px-0' : 'gap-3 px-3',
                      isActive
                        ? 'bg-white/[0.09] text-white'
                        : 'text-white/55 hover:bg-white/[0.05] hover:text-white/90',
                    )
                  }
                >
                  {({ isActive }) => (
                    <>
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
                      <span className={cn('whitespace-nowrap', collapsed && 'sr-only')}>
                        {item.label}
                      </span>
                    </>
                  )}
                </NavLink>
              </RailTip>
            </li>
          ))}
        </ul>
      </nav>

      {/* Account footer */}
      <div className="border-t border-white/[0.08] p-3">
        {loadingUser ? (
          <div className={cn('flex items-center gap-3 py-1.5', collapsed ? 'justify-center' : 'px-2')}>
            <Skeleton variant="circular" width={32} height={32} className="bg-white/10" />
            {!collapsed && <Skeleton width={120} height={12} className="bg-white/10" />}
          </div>
        ) : (
          <RailTip collapsed={collapsed} label={accountLabel}>
            <div
              className={cn(
                'flex min-w-0 items-center gap-3 py-1.5',
                collapsed ? 'justify-center' : 'px-2',
              )}
            >
              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/25 text-[11px] font-semibold text-primary-light">
                {initials || <User size={15} />}
              </span>
              <span className={cn('min-w-0 truncate text-[13px] text-white/60', collapsed && 'sr-only')}>
                {accountLabel}
              </span>
            </div>
          </RailTip>
        )}

        {error && !collapsed && <p className="px-2 pb-1 text-xs text-error-light">{error}</p>}

        <RailTip collapsed={collapsed} label="Sign out">
          <button
            type="button"
            onClick={signOut}
            disabled={isSigningOut}
            className={cn(
              'mt-1 flex w-full items-center rounded-lg py-2 text-[13.5px] font-medium text-white/55 outline-none transition-colors duration-120 hover:bg-white/[0.05] hover:text-white/90 focus-visible:ring-2 focus-visible:ring-primary-light/60 disabled:opacity-50',
              collapsed ? 'justify-center px-0' : 'gap-3 px-3',
            )}
          >
            <LogOut size={17} className="shrink-0 text-white/45" />
            <span className={cn('whitespace-nowrap', collapsed && 'sr-only')}>
              {isSigningOut ? 'Signing out…' : 'Sign out'}
            </span>
          </button>
        </RailTip>

        {/* Pin control — desktop only; overrides the scroll behaviour. */}
        {onTogglePin && (
          <RailTip collapsed={collapsed} label={pinned ? 'Unpin sidebar' : 'Keep sidebar open'}>
            <button
              type="button"
              onClick={onTogglePin}
              aria-pressed={pinned}
              className={cn(
                'mt-1 flex w-full items-center rounded-lg py-2 text-[13.5px] font-medium outline-none transition-colors duration-120 hover:bg-white/[0.05] focus-visible:ring-2 focus-visible:ring-primary-light/60',
                pinned ? 'text-primary-light' : 'text-white/40 hover:text-white/70',
                collapsed ? 'justify-center px-0' : 'gap-3 px-3',
              )}
            >
              {pinned ? (
                <PanelLeftClose size={17} className="shrink-0" />
              ) : (
                <PanelLeftOpen size={17} className="shrink-0" />
              )}
              <span className={cn('whitespace-nowrap', collapsed && 'sr-only')}>
                {pinned ? 'Unpin' : 'Keep open'}
              </span>
            </button>
          </RailTip>
        )}
      </div>
    </div>
  );
};

export default Sidebar;
