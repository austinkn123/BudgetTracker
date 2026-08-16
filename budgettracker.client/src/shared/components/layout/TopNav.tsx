import { NavLink } from 'react-router-dom';
import { LogOut, Menu, User, Wallet } from 'lucide-react';
import { useAuth } from '../../../auth/useAuth';
import { useUser } from '../../../features/user/hooks/useUser';
import { useSignOut } from '../../hooks/useSignOut';
import { Skeleton, Tooltip } from '../ui';
import { cn } from '../../utils/cn';
import { NAV_ITEMS } from './navItems';
import { NAV_HEIGHT, NAV_TOP_OFFSET } from './constants';
import { getAccountLabel, getInitials } from './userIdentity';

export interface TopNavProps {
  /** Slides the bar out of view (driven by scroll direction in AppShell). */
  hidden?: boolean;
  onOpenMobileNav: () => void;
}

/**
 * Floating top navigation (BUD-20).
 *
 * A detached, blurred pill rather than a full-bleed bar — it reads as an
 * overlay on the content instead of a browser chrome strip, and it slides
 * away on scroll so long pages get the vertical space back.
 */
const TopNav = ({ hidden = false, onOpenMobileNav }: TopNavProps) => {
  const { data: user, isLoading: loadingUser } = useUser();
  const { user: cognitoUser } = useAuth();
  const { signOut, isSigningOut } = useSignOut();

  // useUser() is the reliable source: the Cognito email is empty after a page
  // refresh because getCurrentUser() does not repopulate signInDetails.
  const email = user?.email || cognitoUser?.email || '';
  const accountLabel = getAccountLabel(email, cognitoUser?.username);
  const initials = getInitials(email);

  return (
    <div
      className={cn(
        'fixed inset-x-0 z-40 px-4 transition-transform duration-240 ease-out-soft motion-reduce:transition-none',
        hidden && '-translate-y-[calc(100%+var(--nav-top))]',
      )}
      style={{ top: NAV_TOP_OFFSET, ['--nav-top' as string]: `${NAV_TOP_OFFSET}px` }}
    >
      <nav
        aria-label="Main navigation"
        className="mx-auto flex max-w-7xl items-center gap-2 rounded-full bg-grey-900/85 px-2.5 shadow-lg ring-1 ring-white/10 backdrop-blur-xl"
        style={{ height: NAV_HEIGHT }}
      >
        {/* Brand */}
        <NavLink
          to="/"
          className="focus-ring flex shrink-0 items-center gap-2 rounded-full px-2 py-1.5"
          aria-label="BudgetTracker home"
        >
          <span className="flex text-primary-light">
            <Wallet size={20} />
          </span>
          <span className="hidden text-[14.5px] font-semibold tracking-[-0.01em] text-white sm:inline">
            BudgetTracker
          </span>
        </NavLink>

        <span className="mx-1 hidden h-5 w-px bg-white/10 lg:block" aria-hidden />

        {/* Desktop links */}
        <ul className="hidden flex-1 items-center gap-0.5 lg:flex">
          {NAV_ITEMS.map((item) => (
            <li key={item.path}>
              <NavLink
                to={item.path}
                end={item.end}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-2 rounded-full px-3.5 py-2 text-[13.5px] font-medium transition-colors duration-120',
                    'outline-none focus-visible:ring-2 focus-visible:ring-primary-light/60',
                    isActive
                      ? 'bg-white/[0.12] text-white'
                      : 'text-white/55 hover:bg-white/[0.06] hover:text-white/90',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon
                      size={16}
                      className={isActive ? 'text-primary-light' : 'text-white/45'}
                    />
                    {item.label}
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>

        {/* Spacer on mobile so the account cluster stays right-aligned */}
        <span className="flex-1 lg:hidden" />

        {/* Account cluster */}
        <div className="flex shrink-0 items-center gap-1.5">
          {loadingUser ? (
            <Skeleton variant="circular" width={30} height={30} className="bg-white/10" />
          ) : (
            <Tooltip title={accountLabel}>
              <span className="flex h-[30px] w-[30px] items-center justify-center rounded-full bg-primary/25 text-[11px] font-semibold text-primary-light">
                {initials || <User size={14} />}
              </span>
            </Tooltip>
          )}

          <Tooltip title="Sign out">
            <button
              type="button"
              onClick={signOut}
              disabled={isSigningOut}
              aria-label="Sign out"
              className="focus-ring inline-flex h-9 w-9 items-center justify-center rounded-full text-white/45 transition-colors duration-120 hover:bg-white/[0.06] hover:text-white/90 disabled:opacity-50"
            >
              <LogOut size={16} />
            </button>
          </Tooltip>

          {/* Mobile menu trigger */}
          <button
            type="button"
            onClick={onOpenMobileNav}
            aria-label="Open navigation menu"
            className="focus-ring inline-flex h-9 w-9 items-center justify-center rounded-full text-white/70 transition-colors duration-120 hover:bg-white/[0.06] hover:text-white lg:hidden"
          >
            <Menu size={18} />
          </button>
        </div>
      </nav>
    </div>
  );
};

export default TopNav;
