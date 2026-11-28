import { NavLink } from 'react-router-dom';
import { LogOut, MessageSquare, Moon, Settings, Sun, User, Wallet } from 'lucide-react';
import { useAuth } from '../../../auth/useAuth';
import { useUser } from '../../../features/user/hooks/useUser';
import { useSignOut } from '../../hooks/useSignOut';
import { useTheme } from '../../theme/useTheme';
import { Skeleton } from '../ui';
import { cn } from '../../utils/cn';
import { useNavItems } from './navItems';
import { getAccountLabel, getInitials } from './userIdentity';

export interface MobileNavPanelProps {
  onNavigate: () => void;
  /** Opens the Send feedback dialog (the shell closes this sheet first). */
  onOpenFeedback?: () => void;
}

/** Contents of the mobile navigation sheet (BUD-20). */
const MobileNavPanel = ({ onNavigate, onOpenFeedback }: MobileNavPanelProps) => {
  const { data: user, isLoading: loadingUser } = useUser();
  const { user: cognitoUser } = useAuth();
  const { signOut, isSigningOut, error } = useSignOut();
  const { theme, toggle: toggleTheme } = useTheme();
  const navItems = useNavItems();

  const email = user?.email || cognitoUser?.email || '';
  const accountLabel = getAccountLabel(email, cognitoUser?.username);
  const initials = getInitials(email);

  return (
    <div className="flex h-full flex-col bg-surface">
      <div className="flex items-center gap-2.5 px-5 pb-6 pt-6">
        <span className="flex text-primary dark:text-primary-light">
          <Wallet size={22} />
        </span>
        <span className="text-[15px] font-semibold tracking-[-0.01em] text-ink">
          BudgetTracker
        </span>
      </div>

      <nav aria-label="Main navigation" className="flex-1 px-3">
        <ul className="flex flex-col gap-0.5">
          {navItems.map((item) => (
            <li key={item.path}>
              <NavLink
                to={item.path}
                end={item.end}
                onClick={onNavigate}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 rounded-lg px-3 py-2.5 text-[14px] font-medium transition-colors duration-120',
                    'outline-none focus-visible:ring-2 focus-visible:ring-primary-light/60',
                    isActive
                      ? 'bg-ink/[0.09] text-ink'
                      : 'text-ink/55 hover:bg-ink/[0.05] hover:text-ink/90',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon
                      size={18}
                      className={cn('shrink-0', isActive ? 'text-primary dark:text-primary-light' : 'text-ink/45')}
                    />
                    {item.label}
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <div className="border-t border-ink/[0.08] p-3">
        {loadingUser ? (
          <div className="flex items-center gap-3 px-2 py-1.5">
            <Skeleton variant="circular" width={32} height={32} className="bg-ink/10" />
            <Skeleton width={120} height={12} className="bg-ink/10" />
          </div>
        ) : (
          <div className="flex min-w-0 items-center gap-3 px-2 py-1.5">
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/25 text-[11px] font-semibold text-primary dark:text-primary-light">
              {initials || <User size={15} />}
            </span>
            <span className="min-w-0 truncate text-[13px] text-ink/60">{accountLabel}</span>
          </div>
        )}

        {error && <p className="px-2 pb-1 text-xs text-error dark:text-error-light">{error}</p>}

        <NavLink
          to="/settings"
          onClick={onNavigate}
          className={({ isActive }) =>
            cn(
              'mt-1 flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-[14px] font-medium outline-none transition-colors duration-120 focus-visible:ring-2 focus-visible:ring-primary-light/60',
              isActive ? 'bg-ink/[0.09] text-ink' : 'text-ink/55 hover:bg-ink/[0.05] hover:text-ink/90',
            )
          }
        >
          {({ isActive }) => (
            <>
              <Settings size={18} className={cn('shrink-0', isActive ? 'text-primary dark:text-primary-light' : 'text-ink/45')} />
              Settings
            </>
          )}
        </NavLink>

        {onOpenFeedback && (
          <button
            type="button"
            onClick={onOpenFeedback}
            className="mt-1 flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-[14px] font-medium text-ink/55 outline-none transition-colors duration-120 hover:bg-ink/[0.05] hover:text-ink/90 focus-visible:ring-2 focus-visible:ring-primary-light/60"
          >
            <MessageSquare size={18} className="shrink-0 text-ink/45" />
            Send feedback
          </button>
        )}

        <button
          type="button"
          onClick={toggleTheme}
          className="mt-1 flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-[14px] font-medium text-ink/55 outline-none transition-colors duration-120 hover:bg-ink/[0.05] hover:text-ink/90 focus-visible:ring-2 focus-visible:ring-primary-light/60"
        >
          {theme === 'dark' ? (
            <Sun size={18} className="shrink-0 text-ink/45" />
          ) : (
            <Moon size={18} className="shrink-0 text-ink/45" />
          )}
          {theme === 'dark' ? 'Light theme' : 'Dark theme'}
        </button>

        <button
          type="button"
          onClick={signOut}
          disabled={isSigningOut}
          className="mt-1 flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-[14px] font-medium text-ink/55 outline-none transition-colors duration-120 hover:bg-ink/[0.05] hover:text-ink/90 focus-visible:ring-2 focus-visible:ring-primary-light/60 disabled:opacity-50"
        >
          <LogOut size={18} className="shrink-0 text-ink/45" />
          {isSigningOut ? 'Signing out…' : 'Sign out'}
        </button>
      </div>
    </div>
  );
};

export default MobileNavPanel;
