import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';
import { LogOut, Menu, Moon, Sun, User, Wallet } from 'lucide-react';
import { useAuth } from '../../../auth/useAuth';
import { useUser } from '../../../features/user/hooks/useUser';
import { useSignOut } from '../../hooks/useSignOut';
import { useTheme } from '../../theme/useTheme';
import { Skeleton, Tooltip } from '../ui';
import { cn } from '../../utils/cn';
import { NAV_ITEMS } from './navItems';
import {
  NAV_HEIGHT_COMPACT,
  NAV_HEIGHT_FULL,
  NAV_MAX_WIDTH_COMPACT,
  NAV_MAX_WIDTH_FULL,
  NAV_TOP_OFFSET,
} from './constants';
import { getAccountLabel, getInitials } from './userIdentity';

export interface TopNavProps {
  /** Contracted to a compact icon pill (driven by scroll direction). */
  compact?: boolean;
  onOpenMobileNav: () => void;
}

/**
 * Wraps children in a tooltip only while the bar is contracted.
 *
 * The inner span matters: Radix's Trigger uses `asChild`, which clones the
 * child and merges `className` by string concatenation. NavLink takes a
 * *function* className, and cloning would stringify it — silently dropping
 * every class on the link. Cloning a plain span instead keeps NavLink intact.
 */
const CompactTip = ({
  compact,
  label,
  children,
}: {
  compact: boolean;
  label: string;
  children: ReactNode;
}) =>
  compact ? (
    <Tooltip title={label}>
      <span className="inline-flex">{children}</span>
    </Tooltip>
  ) : (
    <>{children}</>
  );

/**
 * Floating top navigation (BUD-20).
 *
 * A detached blurred pill that morphs with scroll: full width with labelled
 * links at rest, contracting to a narrow icon-only pill as you scroll down.
 * It stays reachable the whole time — contracting rather than hiding means
 * navigation is always one click away.
 */
const TopNav = ({ compact = false, onOpenMobileNav }: TopNavProps) => {
  const { data: user, isLoading: loadingUser } = useUser();
  const { user: cognitoUser } = useAuth();
  const { signOut, isSigningOut } = useSignOut();
  const { theme, toggle: toggleTheme } = useTheme();

  // useUser() is the reliable source: the Cognito email is empty after a page
  // refresh because getCurrentUser() does not repopulate signInDetails.
  const email = user?.email || cognitoUser?.email || '';
  const accountLabel = getAccountLabel(email, cognitoUser?.username);
  const initials = getInitials(email);

  /** Label text collapses to zero width rather than unmounting, so the
   *  accessible name survives and the contraction can animate. */
  const labelClasses = cn(
    'overflow-hidden whitespace-nowrap transition-[max-width,opacity] duration-240 ease-out-soft motion-reduce:transition-none',
    compact ? 'max-w-0 opacity-0' : 'max-w-[12rem] opacity-100',
  );

  return (
    <div className="fixed inset-x-0 z-40 px-4" style={{ top: NAV_TOP_OFFSET }}>
      <nav
        aria-label="Main navigation"
        className={cn(
          'mx-auto flex w-full items-center gap-1.5 rounded-full bg-grey-900/85 backdrop-blur-xl',
          'transition-[max-width,height,padding,box-shadow] duration-240 ease-out-soft motion-reduce:transition-none',
          compact ? 'px-1.5 shadow-xl ring-1 ring-white/[0.14]' : 'px-2.5 shadow-lg ring-1 ring-white/10',
        )}
        style={{
          height: compact ? NAV_HEIGHT_COMPACT : NAV_HEIGHT_FULL,
          maxWidth: compact ? NAV_MAX_WIDTH_COMPACT : NAV_MAX_WIDTH_FULL,
        }}
      >
        {/* Brand */}
        <NavLink
          to="/"
          className="focus-ring flex shrink-0 items-center gap-2 rounded-full px-2 py-1.5"
          aria-label="BudgetTracker home"
        >
          <span className="flex text-primary-light">
            <Wallet size={compact ? 18 : 20} />
          </span>
          <span
            className={cn(
              'hidden text-[14.5px] font-semibold tracking-[-0.01em] text-white sm:inline',
              labelClasses,
            )}
          >
            BudgetTracker
          </span>
        </NavLink>

        <span
          className={cn(
            'mx-0.5 hidden h-5 w-px bg-white/10 transition-opacity duration-240 lg:block',
            compact && 'opacity-0',
          )}
          aria-hidden
        />

        {/* Desktop links — centred in the contracted pill, left-aligned beside
            the wordmark when expanded. */}
        <ul
          className={cn(
            'hidden flex-1 items-center gap-0.5 lg:flex',
            compact ? 'justify-center' : 'justify-start',
          )}
        >
          {NAV_ITEMS.map((item) => (
            <li key={item.path}>
              <CompactTip compact={compact} label={item.label}>
                <NavLink
                  to={item.path}
                  end={item.end}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-2 rounded-full py-2 text-[13.5px] font-medium transition-all duration-240 ease-out-soft motion-reduce:transition-none',
                      'outline-none focus-visible:ring-2 focus-visible:ring-primary-light/60',
                      compact ? 'px-2' : 'px-3.5',
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
                        className={cn(
                          'shrink-0',
                          isActive ? 'text-primary-light' : 'text-white/45',
                        )}
                      />
                      <span className={labelClasses}>{item.label}</span>
                    </>
                  )}
                </NavLink>
              </CompactTip>
            </li>
          ))}
        </ul>

        {/* Keeps the account cluster right-aligned below lg */}
        <span className="flex-1 lg:hidden" />

        {/* Account cluster */}
        <div className="flex shrink-0 items-center gap-1">
          <Tooltip title={theme === 'dark' ? 'Switch to light' : 'Switch to dark'}>
            <button
              type="button"
              onClick={toggleTheme}
              aria-label={theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
              className={cn(
                'focus-ring inline-flex items-center justify-center rounded-full text-white/45 transition-all duration-240 ease-out-soft hover:bg-white/[0.06] hover:text-white/90 motion-reduce:transition-none',
                compact ? 'h-8 w-8' : 'h-9 w-9',
              )}
            >
              {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
            </button>
          </Tooltip>

          {loadingUser ? (
            <Skeleton variant="circular" width={28} height={28} className="bg-white/10" />
          ) : (
            <Tooltip title={accountLabel}>
              <span
                className={cn(
                  'flex items-center justify-center rounded-full bg-primary/25 text-[11px] font-semibold text-primary-light transition-all duration-240 ease-out-soft motion-reduce:transition-none',
                  compact ? 'h-7 w-7' : 'h-[30px] w-[30px]',
                )}
              >
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
              className={cn(
                'focus-ring inline-flex items-center justify-center rounded-full text-white/45 transition-all duration-240 ease-out-soft hover:bg-white/[0.06] hover:text-white/90 disabled:opacity-50 motion-reduce:transition-none',
                compact ? 'h-8 w-8' : 'h-9 w-9',
              )}
            >
              <LogOut size={16} />
            </button>
          </Tooltip>

          {/* Mobile menu trigger */}
          <button
            type="button"
            onClick={onOpenMobileNav}
            aria-label="Open navigation menu"
            className={cn(
              'focus-ring inline-flex items-center justify-center rounded-full text-white/70 transition-all duration-240 ease-out-soft hover:bg-white/[0.06] hover:text-white motion-reduce:transition-none lg:hidden',
              compact ? 'h-8 w-8' : 'h-9 w-9',
            )}
          >
            <Menu size={18} />
          </button>
        </div>
      </nav>
    </div>
  );
};

export default TopNav;
