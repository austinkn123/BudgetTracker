import * as RadixSelect from '@radix-ui/react-select';
import { Check, ChevronDown } from 'lucide-react';
import { cn } from '../../utils/cn';
import type { SelectOption } from './Select';

export interface InlineSelectProps<TValue extends string | number = string> {
  value: TValue | null | undefined;
  onChange: (value: TValue | null) => void;
  options: readonly SelectOption<TValue>[];
  /** Leading blank choice, e.g. "Uncategorized". Selecting it emits null. */
  emptyOptionLabel?: string;
  placeholder?: string;
  ariaLabel: string;
  valueAs?: 'string' | 'number';
  disabled?: boolean;
  /** Borderless until hovered — for dense table rows where a full field would be noisy. */
  subtle?: boolean;
  className?: string;
}

/**
 * Standalone select for use outside a form (BUD-20 design system). The sibling `Select`
 * wraps react-hook-form; this one is plain value/onChange, for table rows and toolbars.
 *
 * Radix Select.Item throws on value="", so the empty choice uses a sentinel.
 */
const EMPTY_SENTINEL = '__empty__';

const InlineSelect = <TValue extends string | number = string>({
  value,
  onChange,
  options,
  emptyOptionLabel,
  placeholder = 'Select…',
  ariaLabel,
  valueAs = 'string',
  disabled = false,
  subtle = false,
  className,
}: InlineSelectProps<TValue>) => (
  <RadixSelect.Root
    value={value == null || value === '' ? EMPTY_SENTINEL : String(value)}
    onValueChange={(next) => {
      if (next === EMPTY_SENTINEL) {
        onChange(null);
        return;
      }
      onChange((valueAs === 'number' ? Number(next) : next) as TValue);
    }}
    disabled={disabled}
  >
    <RadixSelect.Trigger
      aria-label={ariaLabel}
      className={cn(
        'focus-ring flex h-8 w-full items-center justify-between gap-1.5 rounded-md px-2 text-[13px] text-ink transition-colors duration-120 disabled:cursor-not-allowed disabled:text-ink-muted',
        'data-[placeholder]:text-ink-muted/60',
        subtle
          ? 'border border-transparent bg-transparent hover:border-border hover:bg-surface'
          : 'border border-border bg-surface shadow-xs hover:border-border-strong',
        className,
      )}
    >
      <span className="truncate">
        <RadixSelect.Value placeholder={placeholder} />
      </span>
      <RadixSelect.Icon>
        <ChevronDown size={14} className="text-ink-muted" />
      </RadixSelect.Icon>
    </RadixSelect.Trigger>

    <RadixSelect.Portal>
      <RadixSelect.Content
        position="popper"
        sideOffset={4}
        className="z-50 max-h-72 min-w-[var(--radix-select-trigger-width)] overflow-y-auto rounded-md border border-border bg-surface p-1 shadow-md animate-fade-in"
      >
        <RadixSelect.Viewport>
          {emptyOptionLabel !== undefined && (
            <InlineSelectItem value={EMPTY_SENTINEL} label={emptyOptionLabel} muted />
          )}
          {options.map((option) => (
            <InlineSelectItem
              key={String(option.value)}
              value={String(option.value)}
              label={option.label}
              disabled={option.disabled}
            />
          ))}
        </RadixSelect.Viewport>
      </RadixSelect.Content>
    </RadixSelect.Portal>
  </RadixSelect.Root>
);

const InlineSelectItem = ({
  value,
  label,
  disabled,
  muted,
}: {
  value: string;
  label: string;
  disabled?: boolean;
  muted?: boolean;
}) => (
  <RadixSelect.Item
    value={value}
    disabled={disabled}
    className={cn(
      'flex cursor-pointer select-none items-center justify-between gap-2 rounded px-2.5 py-1.5 text-sm outline-none transition-colors duration-120',
      'data-[highlighted]:bg-primary-subtle data-[highlighted]:text-primary-dark',
      'data-[disabled]:pointer-events-none data-[disabled]:opacity-50',
      muted ? 'text-ink-muted' : 'text-ink',
    )}
  >
    <RadixSelect.ItemText>{label}</RadixSelect.ItemText>
    <RadixSelect.ItemIndicator>
      <Check size={14} />
    </RadixSelect.ItemIndicator>
  </RadixSelect.Item>
);

export default InlineSelect;
