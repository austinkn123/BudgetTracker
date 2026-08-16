import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { Sheet } from '../ui';
import { useScrollDirection } from '../../hooks/useScrollDirection';
import MobileNavPanel from './MobileNavPanel';
import TopNav from './TopNav';
import { MOBILE_NAV_WIDTH, NAV_HEIGHT, NAV_TOP_OFFSET } from './constants';

/**
 * App shell for authenticated routes (BUD-14, BUD-20).
 *
 * A floating top nav overlays the content and slides away while you scroll
 * down, returning on the way up — a horizontal bar is the one thing that
 * genuinely reclaims vertical space by hiding.
 */
const AppShell = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();
  const { direction, atTop } = useScrollDirection();

  // Never hide while the mobile sheet is open — the trigger must stay put.
  const navHidden = direction === 'down' && !atTop && !mobileOpen;

  // Close the mobile sheet whenever the route changes.
  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  return (
    <div className="min-h-screen bg-background">
      <TopNav hidden={navHidden} onOpenMobileNav={() => setMobileOpen(true)} />

      <Sheet
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        title="Navigation"
        width={MOBILE_NAV_WIDTH}
      >
        <MobileNavPanel onNavigate={() => setMobileOpen(false)} />
      </Sheet>

      {/*
        min-w-0 keeps intrinsically wide descendants (the calendar, the plan
        table) from pushing the page past the viewport — it is the fix for
        horizontal scroll at 375px. Do not remove.
      */}
      <main className="min-w-0">
        <div
          className="mx-auto w-full max-w-7xl px-4 pb-12 sm:px-6 lg:px-8"
          style={{ paddingTop: NAV_HEIGHT + NAV_TOP_OFFSET * 2 + 16 }}
        >
          <Outlet />
        </div>
      </main>
    </div>
  );
};

export default AppShell;
