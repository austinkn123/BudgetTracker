import { useId } from 'react';
import * as RadixSelect from '@radix-ui/react-select';
import { Check, ChevronDown } from 'lucide-react';
import { Controller } from 'react-hook-form';
import type { Control, FieldPath, FieldValues } from 'react-hook-form';
import { cn } from '../../utils/cn';
import FieldShell from './FieldShell';

export interface SelectOption<TValue extends string | number = string> {
  readonly value: TValue;
  readonly label: string;
  readonly disabled?: boolean;
}

export interface SelectProps<
  TFieldValues extends FieldValues,
  TValue extends string | number = string,
> {
  control: Control<TFieldValues>;
  name: FieldPath<TFieldValues>;
  label: string;
  options: readonly SelectOption<TValue>[];
  /** Adds a leading blank choice, e.g. "Uncategorized". */
  emptyOptionLabel?: string;
  /** 'number' coerces the selected value back to a number for the form. */
  valueAs?: 'string' | 'number';
  helperText?: string;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  fullWidth?: boolean;
  className?: string;
}

/**
 * Radix Select.Item throws on value="" (reserved for clearing), so the empty
 * option uses a sentinel translated back at the form boundary.
 */
const EMPTY_SENTINEL = '__empty__';

/** Form select field (BUD-20), built on Radix Select + RHF Controller. */
const Select = <TFieldValues extends FieldValues, TValue extends string | number = string>({
  control,
  name,
  label,
  options,
  emptyOptionLabel,
  valueAs = 'string',
  helperText,
  placeholder = 'Select…',
  required = false,
  disabled = false,
  fullWidth = true,
  className,
}: SelectProps<TFieldValues, TValue>) => {
  const id = useId();

  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <FieldShell
          label={label}
          htmlFor={id}
          required={required}
          error={fieldState.error?.message}
          helperText={helperText}
          fullWidth={fullWidth}
          className={className}
        >
          <RadixSelect.Root
            value={
              field.value == null || field.value === '' ? EMPTY_SENTINEL : String(field.value)
            }
            onValueChange={(value) => {
              if (value === EMPTY_SENTINEL) {
                field.onChange(undefined);
                return;
              }
              field.onChange(valueAs === 'number' ? Number(value) : value);
            }}
            disabled={disabled}
          >
            <RadixSelect.Trigger
              id={id}
              ref={field.ref}
              onBlur={field.onBlur}
              aria-invalid={fieldState.error ? true : undefined}
              className={cn(
                'focus-ring flex h-9 w-full items-center justify-between gap-2 rounded border border-border bg-surface px-3 text-body text-ink shadow-xs transition-colors duration-120 hover:border-border-strong disabled:cursor-not-allowed disabled:bg-background disabled:text-ink-muted',
                'data-[placeholder]:text-ink-muted/70',
                fieldState.error && 'border-error focus-visible:border-error focus-visible:ring-error/25',
              )}
            >
              <span className="truncate">
                <RadixSelect.Value placeholder={placeholder} />
              </span>
              <RadixSelect.Icon>
                <ChevronDown size={16} className="text-ink-muted" />
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
                    <SelectItem value={EMPTY_SENTINEL} label={emptyOptionLabel} muted />
                  )}
                  {options.map((option) => (
                    <SelectItem
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
        </FieldShell>
      )}
    />
  );
};

const SelectItem = ({
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

export default Select;
