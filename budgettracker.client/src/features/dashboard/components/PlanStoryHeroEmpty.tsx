import { Link as RouterLink } from 'react-router-dom';
import { Button } from '../../../shared/components/ui';
import type { PeriodPacing } from '../../../shared/types/api';
import PlanStoryHero from './PlanStoryHero';

// Example figures for the preview only. Labelled on screen, never mixed with real data.
const SAMPLE_PACING: PeriodPacing = {
  daysElapsed: 18,
  daysInMonth: 30,
  daysPct: 0.6,
  spentPct: 0.52,
  projectedEnd: 4052,
  pacingDelta: -0.08,
  status: 'Ahead',
  plannedExpenses: 4678,
  actualExpenses: 2431,
  remaining: 2247,
  perDiemToStay: 187.25,
};

/**
 * Fallback hero when there is no active plan. Rather than an empty box, it shows the real hero
 * rendered with example figures, dimmed behind the call to action, so the reason to make a plan
 * is the thing you'd get. The preview is inert and hidden from assistive tech.
 */
const PlanStoryHeroEmpty = () => (
  <div className="relative isolate overflow-hidden rounded-xl bg-grey-900">
    <div aria-hidden inert className="pointer-events-none select-none opacity-35 blur-[1px]">
      <PlanStoryHero
        plan={{ id: 0, name: 'Example plan', planMonth: '2026-01-01' }}
        analyzedMonth="2026-06-01"
        pacing={SAMPLE_PACING}
        headline="Comfortably ahead in June"
        drifting={[]}
      />
    </div>

    <div className="absolute inset-0 flex items-center justify-center bg-grey-900/55 p-6">
      <div className="max-w-md text-center">
        <p className="text-2xs font-semibold uppercase tracking-[0.08em] text-white/60">
          Preview with example figures
        </p>
        <h2 className="mt-2 text-xl font-semibold tracking-[-0.02em] text-white">
          Make a plan and this becomes your month
        </h2>
        <p className="mt-1.5 text-sm text-white/70">
          Pacing, projection and drifting categories, measured against what you said you'd spend.
        </p>
        <Button component={RouterLink} to="/budget-plans" size="lg" className="mt-5">
          Create a plan
        </Button>
      </div>
    </div>
  </div>
);

export default PlanStoryHeroEmpty;
