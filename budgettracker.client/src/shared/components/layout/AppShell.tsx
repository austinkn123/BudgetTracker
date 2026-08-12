import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Drawer from '@mui/material/Drawer';
import IconButton from '@mui/material/IconButton';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import { Menu, Wallet } from 'lucide-react';
import Sidebar from './Sidebar';
import { MOBILE_TOPBAR_HEIGHT, SIDEBAR_WIDTH } from './constants';

/**
 * Responsive app shell for authenticated routes (BUD-14).
 *
 * Desktop (>= lg / 1024px): permanent sidebar in the flex row.
 * Below that: slim top bar with a hamburger that opens a temporary drawer.
 *
 * Both drawers are always mounted and toggled with `display`, rather than
 * swapping one Drawer's `variant` off a `useMediaQuery`. A JS media query
 * returns false on first render, which flashes the mobile bar on desktop, and
 * changing `variant` remounts the drawer and drops focus.
 */
const AppShell = () => {
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();

  // Close the mobile drawer whenever the route changes.
  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      {/* Desktop: permanent sidebar */}
      <Drawer
        variant="permanent"
        sx={{
          display: { xs: 'none', lg: 'block' },
          width: SIDEBAR_WIDTH,
          flexShrink: 0,
        }}
        slotProps={{
          paper: {
            sx: {
              width: SIDEBAR_WIDTH,
              boxSizing: 'border-box',
              borderRight: 1,
              borderColor: 'divider',
            },
          },
        }}
      >
        <Sidebar />
      </Drawer>

      {/* Mobile / tablet: temporary drawer */}
      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={() => setMobileOpen(false)}
        ModalProps={{ keepMounted: true }}
        sx={{ display: { xs: 'block', lg: 'none' } }}
        slotProps={{ paper: { sx: { width: SIDEBAR_WIDTH, boxSizing: 'border-box' } } }}
      >
        <Sidebar onNavigate={() => setMobileOpen(false)} />
      </Drawer>

      {/*
        minWidth: 0 is what keeps AC 6 true. Without it a flex child's min-width
        resolves to `auto`, so an intrinsically wide descendant (the plan entries
        table, the date calendar) would push the shell past the viewport.
      */}
      <Box sx={{ flexGrow: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <AppBar
          position="sticky"
          elevation={0}
          sx={{
            display: { xs: 'block', lg: 'none' },
            bgcolor: 'background.paper',
            borderBottom: 1,
            borderColor: 'divider',
          }}
        >
          <Toolbar sx={{ minHeight: MOBILE_TOPBAR_HEIGHT, gap: 1.5 }} disableGutters={false}>
            <IconButton
              edge="start"
              aria-label="Open navigation menu"
              onClick={() => setMobileOpen(true)}
              sx={{ color: 'text.secondary' }}
            >
              <Menu size={22} />
            </IconButton>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, color: 'primary.main' }}>
              <Wallet size={20} />
            </Box>
            <Typography variant="h6" sx={{ fontWeight: 700, color: 'text.primary' }}>
              BudgetTracker
            </Typography>
          </Toolbar>
        </AppBar>

        <Box component="main" sx={{ flexGrow: 1, width: '100%' }}>
          {/* Kept verbatim from the previous layout so no page needs edits. */}
          <div className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
            <Outlet />
          </div>
        </Box>
      </Box>
    </Box>
  );
};

export default AppShell;
