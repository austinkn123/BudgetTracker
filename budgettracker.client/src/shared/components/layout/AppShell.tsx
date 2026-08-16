import { useCallback, useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { Menu, Wallet } from 'lucide-react';
import { Sheet } from '../ui';
import { useScrollDirection } from '../../hooks/useScrollDirection';
import { cn } from '../../utils/cn';
import Sidebar from './Sidebar';
import { SIDEBAR_PIN_KEY, SIDEBAR_RAIL_WIDTH, SIDEBAR_WIDTH } from './constants';

const readPinned = (): boolean => {
  try {
    return window.localStorage.getItem(SIDEBAR_PIN_KEY) === 'true';
  } catch {
    // Private mode / storage disabled — fall back to unpinned.
    return false;
  }
};

/**
 * Responsive app shell for authenticated routes (BUD-14, BUD-20).
 *
 * Desktop (>= lg / 1024px): the rail collapses to icons as you scroll down and
 * expands when you scroll back up or reach the top — giving content the width
 * while you read, and the full labels while you navigate. A pin overrides it.
 *
 * Below lg: slim top bar that slides away on scroll down, plus a Sheet drawer.
 */
const AppShell = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [pinned, setPinned] = useState(readPinned);
  const location = useLocation();
  const { direction, atTop } = useScrollDirection();

  // Pinned open wins; otherwise collapse while scrolling down away from the top.
  const collapsed = !pinned && direction === 'down' && !atTop;
  // The top bar only hides once you are past the header and heading down.
  const hideTopBar = direction === 'down' && !atTop;

  // Close the mobile sheet whenever the route changes.
  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  const togglePin = useCallback(() => {
    setPinned((prev) => {
      const next = !prev;
      try {
        window.localStorage.setItem(SIDEBAR_PIN_KEY, String(next));
      } catch {
        // Persistence is best-effort; the toggle still works this session.
      }
      return next;
    });
  }, []);

  return (
    <div className="flex min-h-screen bg-background">
      {/* Desktop: scroll-aware rail. bg on the <aside> too, so the column still
          reads as one surface when the page scrolls past a viewport. */}
      <aside
        className="hidden shrink-0 bg-grey-900 transition-[width] duration-240 ease-out-soft motion-reduce:transition-none lg:block"
        style={{ width: collapsed ? SIDEBAR_RAIL_WIDTH : SIDEBAR_WIDTH }}
      >
        <div className="sticky top-0 h-screen">
          <Sidebar collapsed={collapsed} pinned={pinned} onTogglePin={togglePin} />
        </div>
      </aside>

      {/* Mobile / tablet: slide-over sheet */}
      <Sheet
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        title="Navigation"
        width={SIDEBAR_WIDTH}
      >
        <Sidebar onNavigate={() => setMobileOpen(false)} />
      </Sheet>

      {/*
        min-w-0 keeps wide descendants (calendar, plan table) from pushing the
        shell past the viewport — it is the entire fix for horizontal scroll
        at 375px. Do not remove.
      */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header
          className={cn(
            'sticky top-0 z-30 border-b border-border bg-surface/85 backdrop-blur transition-transform duration-240 ease-out-soft motion-reduce:transition-none lg:hidden',
            hideTopBar && '-translate-y-full',
          )}
        >
          <div className="flex h-14 items-center gap-3 px-4">
            <button
              type="button"
              aria-label="Open navigation menu"
              onClick={() => setMobileOpen(true)}
              className="focus-ring -ml-1 inline-flex h-9 w-9 items-center justify-center rounded-lg text-ink-muted transition-colors duration-120 hover:bg-border-subtle hover:text-ink"
            >
              <Menu size={22} />
            </button>
            <span className="flex items-center gap-2 text-primary">
              <Wallet size={20} />
            </span>
            <span className="text-[15px] font-semibold tracking-[-0.01em] text-ink">
              BudgetTracker
            </span>
          </div>
        </header>

        <main className="w-full flex-1">
          <div className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
};

export default AppShell;
