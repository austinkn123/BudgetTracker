import {
  ArrowLeftRight,
  ChartCandlestick,
  ChartNoAxesCombined,
  LayoutDashboard,
  ListChecks,
  Waypoints,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useWebull } from '../../../features/webull/hooks/useWebull';

export interface NavItem {
  readonly label: string;
  readonly path: string;
  readonly icon: LucideIcon;
  /**
   * Passed to `NavLink`. Only the index route needs exact matching; every other
   * item relies on prefix matching so nested routes (e.g. /transactions/:id)
   * keep their parent highlighted.
   */
  readonly end: boolean;
}

const INVESTMENTS_PATH = '/investments';

/**
 * Primary navigation for authenticated routes, in display order. Read it through useNavItems.
 * Settings is not here: it sits with the account controls in TopNav and MobileNavPanel.
 */
const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Money Map', path: '/', icon: Waypoints, end: true },
  { label: 'Budget', path: '/budget', icon: ListChecks, end: false },
  { label: 'Dashboard', path: '/dashboard', icon: LayoutDashboard, end: false },
  { label: 'Cash Flow', path: '/cash-flow', icon: ChartNoAxesCombined, end: false },
  { label: 'Transactions', path: '/transactions', icon: ArrowLeftRight, end: false },
  { label: 'Investments', path: INVESTMENTS_PATH, icon: ChartCandlestick, end: false },
];

const WITHOUT_INVESTMENTS = NAV_ITEMS.filter((item) => item.path !== INVESTMENTS_PATH);

/**
 * The nav for this person. Investments appears only once Webull is known to be set up: hidden
 * while loading (so it never flashes in and back out), when not set up, and if the read fails.
 */
export const useNavItems = (): readonly NavItem[] => {
  const { data } = useWebull();
  return data && data.state !== 'NotSetUp' ? NAV_ITEMS : WITHOUT_INVESTMENTS;
};
