import { NavLink } from 'react-router-dom';
import Avatar from '@mui/material/Avatar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Skeleton from '@mui/material/Skeleton';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import { LogOut, User, Wallet } from 'lucide-react';
import { useAuth } from '../../../auth/useAuth';
import { useUser } from '../../../features/user/hooks/useUser';
import { useSignOut } from '../../hooks/useSignOut';
import { NAV_ITEMS } from './navItems';
import { getAccountLabel, getInitials } from './userIdentity';

export interface SidebarProps {
  /** Called after a nav link is followed, so AppShell can close the mobile drawer. */
  onNavigate?: () => void;
}

const Sidebar = ({ onNavigate }: SidebarProps) => {
  const { data: user, isLoading: loadingUser } = useUser();
  const { user: cognitoUser } = useAuth();
  const { signOut, isSigningOut, error } = useSignOut();

  // useUser() is the reliable source: the Cognito email is empty after a page
  // refresh because getCurrentUser() does not repopulate signInDetails.
  const email = user?.email || cognitoUser?.email || '';
  const accountLabel = getAccountLabel(email, cognitoUser?.username);
  const initials = getInitials(email);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%', bgcolor: 'background.paper' }}>
      {/* Brand */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, px: 2.5, py: 2.5 }}>
        <Box sx={{ display: 'flex', color: 'primary.main' }}>
          <Wallet size={24} />
        </Box>
        <Typography variant="h6" sx={{ fontWeight: 700, color: 'text.primary' }}>
          BudgetTracker
        </Typography>
      </Box>

      <Divider />

      {/* Primary navigation */}
      <List component="nav" aria-label="Main navigation" sx={{ flexGrow: 1, px: 1.5, py: 2 }}>
        {NAV_ITEMS.map((item) => (
          <ListItem key={item.path} disablePadding sx={{ mb: 0.5 }}>
            <ListItemButton
              component={NavLink}
              to={item.path}
              end={item.end}
              onClick={onNavigate}
              sx={{
                borderRadius: 2,
                px: 1.5,
                py: 1,
                color: 'text.secondary',
                '&:hover': { bgcolor: 'grey.100' },
                // NavLink adds `.active` itself, and sets aria-current="page".
                '&.active': {
                  bgcolor: 'primary.subtle',
                  color: 'primary.main',
                  '&:hover': { bgcolor: 'primary.subtle' },
                  '& .MuiListItemIcon-root': { color: 'primary.main' },
                  '& .MuiListItemText-primary': { fontWeight: 600 },
                },
              }}
            >
              <ListItemIcon sx={{ minWidth: 36, color: 'inherit' }}>
                <item.icon size={20} />
              </ListItemIcon>
              <ListItemText primary={item.label} slotProps={{ primary: { fontSize: '0.9375rem' } }} />
            </ListItemButton>
          </ListItem>
        ))}
      </List>

      <Divider />

      {/* Account footer */}
      <Box sx={{ px: 2, py: 2 }}>
        {loadingUser ? (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 1.5 }}>
            <Skeleton variant="circular" width={36} height={36} />
            <Skeleton variant="text" width={130} height={20} />
          </Box>
        ) : (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 1.5, minWidth: 0 }}>
            <Avatar
              sx={{
                width: 36,
                height: 36,
                fontSize: '0.8125rem',
                fontWeight: 600,
                bgcolor: 'primary.subtle',
                color: 'primary.main',
              }}
            >
              {initials || <User size={18} />}
            </Avatar>
            <Tooltip title={accountLabel} placement="top">
              <Typography
                variant="body2"
                sx={{
                  minWidth: 0,
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  whiteSpace: 'nowrap',
                  color: 'text.secondary',
                }}
              >
                {accountLabel}
              </Typography>
            </Tooltip>
          </Box>
        )}

        {error && (
          <Typography variant="caption" sx={{ display: 'block', mb: 1, color: 'error.main' }}>
            {error}
          </Typography>
        )}

        <Button
          fullWidth
          variant="outlined"
          color="error"
          size="small"
          startIcon={<LogOut size={16} />}
          onClick={signOut}
          disabled={isSigningOut}
        >
          {isSigningOut ? 'Signing out…' : 'Sign out'}
        </Button>
      </Box>
    </Box>
  );
};

export default Sidebar;
