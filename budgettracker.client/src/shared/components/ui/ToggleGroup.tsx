import * as RadixToggleGroup from '@radix-ui/react-toggle-group';
import { cn } from '../../utils/cn';

export interface ToggleGroupOption<TValue extends string = string> {
  readonly value: TValue;
  readonly label: string;
}

export interface ToggleGroupProps<TValue extends string = string> {
  value: TValue;
  onChange: (value: TValue) => void;
  options: readonly ToggleGroupOption<TValue>[];
  ariaLabel?: string;
  className?: string;
}

/**
 * Single-select segmented control (BUD-20). Replaces MUI ToggleButtonGroup;
 * Radix supplies roving-tabindex keyboard navigation.
 */
const ToggleGroup = <TValue extends string = string>({
  value,
  onChange,
  options,
  ariaLabel,
  className,
}: ToggleGroupProps<TValue>) => (
  <RadixToggleGroup.Root
    type="single"
    value={value}
    onValueChange={(next) => {
      // Radix emits '' when the active item is clicked again; keep a selection.
      if (next) onChange(next as TValue);
    }}
    aria-label={ariaLabel}
    className={cn(
      'inline-flex items-center gap-0.5 rounded-md border border-border bg-background p-0.5 shadow-xs',
      className,
    )}
  >
    {options.map((option) => (
      <RadixToggleGroup.Item
        key={option.value}
        value={option.value}
        className={cn(
          'focus-ring flex-1 whitespace-nowrap rounded px-3 py-1.5 text-xs font-semibold text-ink-muted transition-colors duration-120',
          'data-[state=on]:bg-surface data-[state=on]:text-ink data-[state=on]:shadow-xs',
          'hover:text-ink',
        )}
      >
        {option.label}
      </RadixToggleGroup.Item>
    ))}
  </RadixToggleGroup.Root>
);

export default ToggleGroup;
