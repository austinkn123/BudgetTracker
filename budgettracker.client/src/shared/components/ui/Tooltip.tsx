import type { ReactNode } from 'react';
import * as RadixTooltip from '@radix-ui/react-tooltip';

export interface TooltipProps {
  title: ReactNode;
  children: ReactNode;
}

/**
 * Hover/focus tooltip (BUD-20). Requires the Tooltip.Provider mounted in
 * main.tsx. Children must be focusable; wrap plain elements in a span.
 */
const Tooltip = ({ title, children }: TooltipProps) => (
  <RadixTooltip.Root>
    <RadixTooltip.Trigger asChild>{children}</RadixTooltip.Trigger>
    <RadixTooltip.Portal>
      <RadixTooltip.Content
        sideOffset={6}
        className="z-50 max-w-xs rounded border border-border bg-ink px-2.5 py-1.5 text-xs font-medium text-white shadow-md animate-fade-in"
      >
        {title}
        <RadixTooltip.Arrow className="fill-ink" />
      </RadixTooltip.Content>
    </RadixTooltip.Portal>
  </RadixTooltip.Root>
);

export default Tooltip;
