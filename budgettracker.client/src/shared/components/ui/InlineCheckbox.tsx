import * as RadixCheckbox from '@radix-ui/react-checkbox';
import { Check, Minus } from 'lucide-react';
import { cn } from '../../utils/cn';

export interface InlineCheckboxProps {
  /** true, false, or 'indeterminate' for a partially-selected "select all". */
  checked: boolean | 'indeterminate';
  onCheckedChange: (checked: boolean) => void;
  ariaLabel: string;
  disabled?: boolean;
  className?: string;
}

/**
 * Standalone checkbox for selection UI (BUD-20 design system). The sibling `Checkbox`
 * wraps react-hook-form and renders a label; this one is plain checked/onChange for
 * table row selection, and supports the indeterminate header state.
 */
const InlineCheckbox = ({
  checked,
  onCheckedChange,
  ariaLabel,
  disabled = false,
  className,
}: InlineCheckboxProps) => (
  <RadixCheckbox.Root
    checked={checked}
    onCheckedChange={(next) => onCheckedChange(next === true)}
    disabled={disabled}
    aria-label={ariaLabel}
    className={cn(
      'focus-ring flex h-4 w-4 shrink-0 items-center justify-center rounded border border-border-strong bg-surface transition-colors duration-120',
      'data-[state=checked]:border-primary data-[state=checked]:bg-primary',
      'data-[state=indeterminate]:border-primary data-[state=indeterminate]:bg-primary',
      'disabled:cursor-not-allowed disabled:opacity-50',
      className,
    )}
  >
    <RadixCheckbox.Indicator className="text-white">
      {checked === 'indeterminate' ? <Minus size={12} /> : <Check size={12} />}
    </RadixCheckbox.Indicator>
  </RadixCheckbox.Root>
);

export default InlineCheckbox;
