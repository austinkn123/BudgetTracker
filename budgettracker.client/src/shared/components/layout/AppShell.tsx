import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { Menu, Wallet } from 'lucide-react';
import { Sheet } from '../ui';
import Sidebar from './Sidebar';
import { SIDEBAR_WIDTH } from './constants';

/**
 * Responsive app shell for authenticated routes (BUD-14, rebuilt in BUD-20).
 *
 * Desktop (>= lg / 1024px): permanent sidebar in the flex row.
 * Below that: slim sticky top bar with a hamburger that opens a Sheet.
 */
const AppShell = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();

  // Close the mobile sheet whenever the route changes.
  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  return (
    <div className="flex min-h-screen bg-background">
      {/* Desktop: permanent sidebar. bg on the <aside> too, so the rail still
          reads as one column when the page scrolls past one viewport. */}
      <aside className="hidden shrink-0 bg-grey-900 lg:block" style={{ width: SIDEBAR_WIDTH }}>
        <div className="sticky top-0 h-screen">
          <Sidebar />
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
        <header className="sticky top-0 z-30 border-b border-border bg-surface lg:hidden">
          <div className="flex h-14 items-center gap-3 px-4">
            <button
              type="button"
              aria-label="Open navigation menu"
              onClick={() => setMobileOpen(true)}
              className="focus-ring -ml-1 inline-flex h-9 w-9 items-center justify-center rounded text-ink-muted transition-colors duration-120 hover:bg-border-subtle hover:text-ink"
            >
              <Menu size={22} />
            </button>
            <span className="flex items-center gap-2 text-primary">
              <Wallet size={20} />
            </span>
            <span className="text-base font-semibold tracking-tight text-ink">BudgetTracker</span>
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
