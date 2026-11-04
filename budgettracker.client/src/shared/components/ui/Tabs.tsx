import { useId, type KeyboardEvent, type ReactNode } from 'react';
import { useSlidingIndicator } from '../../hooks/useSlidingIndicator';
import { cn } from '../../utils/cn';

export interface TabsOption<TValue extends string = string> {
  readonly value: TValue;
  readonly label: string;
}

export interface TabsProps<TValue extends string = string> {
  value: TValue;
  onChange: (value: TValue) => void;
  options: readonly TabsOption<TValue>[];
  ariaLabel: string;
  /** The selected tab's panel. Only it is rendered, so an unselected panel never mounts or fetches. */
  children: ReactNode;
  className?: string;
}

/**
 * Page-level sections (WAI-ARIA tabs). For a choice that filters one view, use `ToggleGroup`.
 * Arrow keys, Home and End move focus and select together: panels are cheap to switch.
 * One underline slides to the selected tab, like the segmented control's thumb.
 */
const Tabs = <TValue extends string = string>({
  value,
  onChange,
  options,
  ariaLabel,
  children,
  className,
}: TabsProps<TValue>) => {
  const id = useId();
  const tabId = (v: TValue) => `${id}-tab-${v}`;
  const panelId = `${id}-panel`;
  const { containerRef, rect } = useSlidingIndicator<HTMLDivElement>('[aria-selected="true"]', value);

  const onKeyDown = (event: KeyboardEvent) => {
    const index = options.findIndex((o) => o.value === value);
    const moves: Partial<Record<string, number>> = {
      ArrowRight: index + 1,
      ArrowLeft: index - 1,
      Home: 0,
      End: options.length - 1,
    };
    const next = moves[event.key];
    if (next === undefined) return;
    event.preventDefault();
    const target = options[(next + options.length) % options.length].value;
    onChange(target);
    document.getElementById(tabId(target))?.focus();
  };

  return (
    <div className={className}>
      <div
        ref={containerRef}
        role="tablist"
        aria-label={ariaLabel}
        onKeyDown={onKeyDown}
        className="relative flex gap-6 border-b border-border-subtle"
      >
        {options.map((option) => {
          const selected = option.value === value;
          return (
            <button
              key={option.value}
              type="button"
              role="tab"
              id={tabId(option.value)}
              aria-selected={selected}
              aria-controls={selected ? panelId : undefined}
              tabIndex={selected ? 0 : -1}
              onClick={() => onChange(option.value)}
              className={cn(
                'focus-ring rounded-sm pb-2.5 pt-1 text-sm font-semibold transition-colors duration-120',
                selected ? 'text-ink' : 'text-ink-muted hover:text-ink',
              )}
            >
              {option.label}
            </button>
          );
        })}
        {rect && (
          <span
            aria-hidden
            className="absolute -bottom-px left-0 h-0.5 rounded-full bg-primary transition-[transform,width] duration-240 ease-out-soft"
            // Measured geometry: runtime pixel values no utility class can express.
            style={{ transform: `translateX(${rect.left}px)`, width: rect.width }}
          />
        )}
      </div>
      <div
        role="tabpanel"
        id={panelId}
        aria-labelledby={tabId(value)}
        // Lets keyboard users reach a panel whose first content isn't focusable (a table).
        tabIndex={0}
        className="focus-ring mt-6 rounded-sm"
      >
        {children}
      </div>
    </div>
  );
};

export default Tabs;
