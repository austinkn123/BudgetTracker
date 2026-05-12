import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Box from '@mui/material/Box';
import { useTheme } from '@mui/material/styles';
import { Link as RouterLink } from 'react-router-dom';
import { Wallet } from 'lucide-react';
import { getHeroSurface, getSemanticColors } from '../utils/chartTheme';

/**
 * Fallback hero rendered when the user has no active budget plan.
 * Mirrors the live hero's visual weight so the dashboard layout doesn't jump.
 */
export function PlanStoryHeroEmpty() {
  const theme = useTheme();
  const surface = getHeroSurface(theme);
  const semantic = getSemanticColors(theme);

  return (
    <Card
      sx={{
        background: surface.background,
        border: `1px solid ${surface.border}`,
        overflow: 'hidden',
      }}
    >
      <CardContent
        sx={{
          p: { xs: 4, md: 6 },
          display: 'flex',
          flexDirection: { xs: 'column', md: 'row' },
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 3,
        }}
      >
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 3 }}>
          <Box
            sx={{
              width: 64,
              height: 64,
              borderRadius: '50%',
              backgroundColor: semantic.neutral,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: semantic.ink,
              flexShrink: 0,
            }}
          >
            <Wallet size={32} />
          </Box>
          <Box>
            <Typography variant="h5" sx={{ fontWeight: 700, color: semantic.ink }}>
              Set up a budget plan to unlock this view
            </Typography>
            <Typography variant="body2" sx={{ color: semantic.expense, mt: 0.5 }}>
              We'll show your pacing, projections, and drifting categories the
              moment an active plan exists.
            </Typography>
          </Box>
        </Box>
        <Button
          component={RouterLink}
          to="/budget-plans"
          variant="contained"
          size="large"
          sx={{ flexShrink: 0 }}
        >
          Create a plan
        </Button>
      </CardContent>
    </Card>
  );
}
