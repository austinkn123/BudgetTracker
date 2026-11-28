import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import FeedbackDialog from '../../../features/feedback/components/FeedbackDialog';
import ErrorBoundary from '../ErrorBoundary';
import { Sheet } from '../ui';
import { useDataVersionSync } from '../../hooks/useDataVersionSync';
import { useScrollDirection } from '../../hooks/useScrollDirection';
import MobileNavPanel from './MobileNavPanel';
import TopNav from './TopNav';
import { MOBILE_NAV_WIDTH, NAV_HEIGHT_FULL, NAV_TOP_OFFSET } from './constants';

/**
 * App shell for authenticated routes (BUD-14, BUD-20).
 *
 * A floating top nav overlays the content and contracts to a compact icon
 * pill while you scroll down, expanding again on the way up. It never leaves
 * the screen, so navigation stays one click away at any scroll depth.
 */
const AppShell = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [feedbackOpen, setFeedbackOpen] = useState(false);
  const location = useLocation();
  const { direction, atTop } = useScrollDirection();
  // One cheap check keeps every authenticated page's budget data fresh (replaces per-query polling).
  useDataVersionSync();

  // Stay expanded while the mobile sheet is open — the trigger must not move.
  const navCompact = direction === 'down' && !atTop && !mobileOpen;

  // Close the mobile sheet whenever the route changes.
  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  return (
    <div className="min-h-screen bg-background">
      <TopNav
        compact={navCompact}
        onOpenMobileNav={() => setMobileOpen(true)}
        onOpenFeedback={() => setFeedbackOpen(true)}
      />

      <Sheet
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        title="Navigation"
        width={MOBILE_NAV_WIDTH}
      >
        <MobileNavPanel
          onNavigate={() => setMobileOpen(false)}
          onOpenFeedback={() => {
            setMobileOpen(false);
            setFeedbackOpen(true);
          }}
        />
      </Sheet>

      {/* Sent with the path the user is on, so the feedback says what they were looking at. */}
      <FeedbackDialog open={feedbackOpen} onClose={() => setFeedbackOpen(false)} page={location.pathname} />

      {/*
        min-w-0 keeps intrinsically wide descendants (the calendar, the plan
        table) from pushing the page past the viewport — it is the fix for
        horizontal scroll at 375px. Do not remove.
      */}
      <main className="min-w-0">
        <div
          className="mx-auto w-full max-w-7xl px-4 pb-12 sm:px-6 lg:px-8"
          // Clear the bar at its tallest so content never jumps as it morphs.
          style={{ paddingTop: NAV_HEIGHT_FULL + NAV_TOP_OFFSET * 2 + 16 }}
        >
          {/* Per-route boundary: a broken page keeps the nav usable, and navigating clears it. */}
          <ErrorBoundary resetKey={location.pathname}>
            <Outlet />
          </ErrorBoundary>
        </div>
      </main>
    </div>
  );
};

export default AppShell;
