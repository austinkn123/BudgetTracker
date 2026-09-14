import { Link as RouterLink } from 'react-router-dom';
import { Wallet } from 'lucide-react';
import { Button, Card } from '../../../shared/components/ui';
import { heroSurface } from '../utils/chartTheme';

/**
 * Fallback hero rendered when the user has no active budget plan.
 * Mirrors the live hero's visual weight so the dashboard layout doesn't jump.
 */
const PlanStoryHeroEmpty = () => (
  <Card padding="none" className="overflow-hidden">
    <div
      className="flex flex-col items-center justify-between gap-6 p-8 md:flex-row md:p-12"
      style={{ background: heroSurface.background }}
    >
      <div className="flex items-center gap-6">
        <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-white/10 text-white">
          <Wallet size={32} />
        </div>
        <div>
          <h2 className="text-xl font-semibold tracking-[-0.02em] text-white">
            Set up a budget plan to unlock this view
          </h2>
          <p className="mt-1.5 text-sm text-white/60">
            We'll show your pacing, projections, and drifting categories the moment an active plan
            exists.
          </p>
        </div>
      </div>
      <Button component={RouterLink} to="/budget-plans" size="lg" className="shrink-0">
        Create a plan
      </Button>
    </div>
  </Card>
);

export default PlanStoryHeroEmpty;
