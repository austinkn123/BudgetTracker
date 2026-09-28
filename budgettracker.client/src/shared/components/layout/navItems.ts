import { ArrowLeftRight, ClipboardList, LayoutDashboard, Settings } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

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

/** Primary navigation for authenticated routes, in display order. */
export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Dashboard', path: '/', icon: LayoutDashboard, end: true },
  { label: 'Budget Plans', path: '/budget-plans', icon: ClipboardList, end: false },
  { label: 'Transactions', path: '/transactions', icon: ArrowLeftRight, end: false },
  { label: 'Settings', path: '/settings', icon: Settings, end: false },
];
