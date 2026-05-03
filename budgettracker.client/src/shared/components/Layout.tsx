import { useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Drawer from '@mui/material/Drawer';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Divider from '@mui/material/Divider';
import { Menu, LayoutDashboard, ArrowLeftRight, ClipboardList, Settings, Wallet } from 'lucide-react';

const NAV_ITEMS = [
  { label: 'Dashboard', path: '/', icon: LayoutDashboard },
  { label: 'Budget Plans', path: '/budget-plans', icon: ClipboardList },
  { label: 'Transactions', path: '/transactions', icon: ArrowLeftRight },
  { label: 'Settings', path: '/settings', icon: Settings },
] as const;

export const Layout = () => {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();

  const handleNav = (path: string) => {
    navigate(path);
    setDrawerOpen(false);
  };

  return (
    <Box className="min-h-screen bg-gray-50">
      <AppBar position="sticky" elevation={0} sx={{ bgcolor: 'white', borderBottom: '1px solid #e5e7eb' }}>
        <Toolbar className="max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8">
          {/* Mobile menu button */}
          <IconButton
            edge="start"
            aria-label="open menu"
            onClick={() => setDrawerOpen(true)}
            sx={{ mr: 1, display: { md: 'none' }, color: 'grey.700' }}
          >
            <Menu className="w-6 h-6" />
          </IconButton>

          {/* Logo / brand */}
          <Box
            className="flex items-center gap-2 cursor-pointer"
            onClick={() => handleNav('/')}
            sx={{ flexGrow: { xs: 1, md: 0 }, mr: { md: 4 } }}
          >
            <Wallet className="w-6 h-6" style={{ color: 'var(--mui-palette-primary-main)' }} />
            <Typography variant="h6" sx={{ color: 'grey.900', fontWeight: 700 }}>
              BudgetTracker
            </Typography>
          </Box>

          {/* Desktop nav links */}
          <Box sx={{ display: { xs: 'none', md: 'flex' }, gap: 0.5, flexGrow: 1 }}>
            {NAV_ITEMS.map((item) => {
              const isActive = location.pathname === item.path;
              return (
                <Button
                  key={item.path}
                  onClick={() => handleNav(item.path)}
                  startIcon={<item.icon className="w-4 h-4" />}
                  sx={{
                    textTransform: 'none',
                    color: isActive ? 'primary.main' : 'grey.600',
                    fontWeight: isActive ? 600 : 400,
                    bgcolor: isActive ? 'primary.50' : 'transparent',
                    borderRadius: 2,
                    px: 2,
                    '&:hover': {
                      bgcolor: isActive ? 'primary.50' : 'grey.100',
                    },
                  }}
                >
                  {item.label}
                </Button>
              );
            })}
          </Box>
        </Toolbar>
      </AppBar>

      {/* Mobile drawer */}
      <Drawer
        anchor="left"
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        PaperProps={{ sx: { width: 280 } }}
      >
        <Box className="p-4 flex items-center gap-2">
          <Wallet className="w-6 h-6" style={{ color: 'var(--mui-palette-primary-main)' }} />
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            BudgetTracker
          </Typography>
        </Box>
        <Divider />
        <List>
          {NAV_ITEMS.map((item) => {
            const isActive = location.pathname === item.path;
            return (
              <ListItem key={item.path} disablePadding>
                <ListItemButton
                  onClick={() => handleNav(item.path)}
                  selected={isActive}
                  sx={{
                    mx: 1,
                    borderRadius: 2,
                    '&.Mui-selected': {
                      bgcolor: 'primary.50',
                      color: 'primary.main',
                      '&:hover': { bgcolor: 'primary.50' },
                    },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 36, color: isActive ? 'primary.main' : 'grey.500' }}>
                    <item.icon className="w-5 h-5" />
                  </ListItemIcon>
                  <ListItemText
                    primary={item.label}
                    primaryTypographyProps={{ fontWeight: isActive ? 600 : 400 }}
                  />
                </ListItemButton>
              </ListItem>
            );
          })}
        </List>
      </Drawer>

      {/* Page content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <Outlet />
      </main>
    </Box>
  );
};
