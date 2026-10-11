import * as RadixToggleGroup from '@radix-ui/react-toggle-group';
import { useSlidingIndicator } from '../../hooks/useSlidingIndicator';
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
 * Radix supplies roving-tabindex keyboard navigation. One thumb slides behind
 * the active item rather than each item painting its own.
 */
const ToggleGroup = <TValue extends string = string>({
  value,
  onChange,
  options,
  ariaLabel,
  className,
}: ToggleGroupProps<TValue>) => {
  const { containerRef, rect } = useSlidingIndicator<HTMLDivElement>('[data-state="on"]', value);

  return (
    <RadixToggleGroup.Root
      ref={containerRef}
      type="single"
      value={value}
      onValueChange={(next) => {
        // Radix emits '' when the active item is clicked again; keep a selection.
        if (next) onChange(next as TValue);
      }}
      aria-label={ariaLabel}
      className={cn(
        'relative isolate inline-flex items-center gap-0.5 rounded-md border border-border bg-background p-0.5 shadow-xs',
        className,
      )}
    >
      {rect && (
        <span
          aria-hidden
          className="absolute left-0 top-0 -z-10 rounded bg-surface shadow-xs transition-[transform,width,height] duration-240 ease-out-soft"
          // Measured geometry: runtime pixel values no utility class can express.
          style={{
            transform: `translate(${rect.left}px, ${rect.top}px)`,
            width: rect.width,
            height: rect.height,
          }}
        />
      )}
      {options.map((option) => (
        <RadixToggleGroup.Item
          key={option.value}
          value={option.value}
          className={cn(
            'focus-ring flex-1 whitespace-nowrap rounded px-3 py-1.5 text-xs font-semibold text-ink-muted transition-colors duration-120',
            'data-[state=on]:text-ink',
            'hover:text-ink',
          )}
        >
          {option.label}
        </RadixToggleGroup.Item>
      ))}
    </RadixToggleGroup.Root>
  );
};

export default ToggleGroup;
