import { useId } from 'react';
import type { ReactNode } from 'react';
import { Controller } from 'react-hook-form';
import type { Control, FieldPath, FieldValues } from 'react-hook-form';
import { cn } from '../../utils/cn';
import FieldShell from './FieldShell';

export type InputValueAs = 'string' | 'number';

export type InputType =
  | 'text'
  | 'email'
  | 'password'
  | 'number'
  | 'tel'
  | 'url'
  | 'date'
  | 'month'
  | 'time'
  | 'datetime-local';

export interface InputProps<TFieldValues extends FieldValues> {
  control: Control<TFieldValues>;
  name: FieldPath<TFieldValues>;
  label: string;
  type?: InputType;
  /** 'number' coerces on change: empty -> undefined, otherwise Number(value). */
  valueAs?: InputValueAs;
  /** Static hint. The validation message takes precedence when the field errors. */
  helperText?: string;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  fullWidth?: boolean;
  autoFocus?: boolean;
  autoComplete?: string;
  multiline?: boolean;
  rows?: number;
  min?: number;
  max?: number;
  step?: number | string;
  maxLength?: number;
  pattern?: string;
  startAdornment?: ReactNode;
  endAdornment?: ReactNode;
  className?: string;
}

const CONTROL_CLASSES =
  'focus-ring w-full rounded-lg border border-border bg-surface text-[14px] text-ink shadow-xs transition-colors duration-120 placeholder:text-ink-muted/60 hover:border-border-strong disabled:cursor-not-allowed disabled:bg-background disabled:text-ink-muted';

const ERROR_CLASSES = 'border-error focus-visible:border-error focus-visible:ring-error/25';

/**
 * Form text field (BUD-20). Wraps React Hook Form's Controller internally, so
 * call sites never wire `error`/`helperText` by hand.
 */
const Input = <TFieldValues extends FieldValues>({
  control,
  name,
  label,
  type = 'text',
  valueAs = 'string',
  helperText,
  placeholder,
  required = false,
  disabled = false,
  fullWidth = true,
  autoFocus = false,
  autoComplete,
  multiline = false,
  rows = 3,
  min,
  max,
  step,
  maxLength,
  pattern,
  startAdornment,
  endAdornment,
  className,
}: InputProps<TFieldValues>) => {
  const id = useId();

  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => {
        const handleChange = (value: string) => {
          if (valueAs === 'number') {
            field.onChange(value === '' ? undefined : Number(value));
            return;
          }
          field.onChange(value);
        };

        const sharedProps = {
          id,
          name: field.name,
          value: (field.value ?? '') as string | number,
          onBlur: field.onBlur,
          placeholder,
          required,
          disabled,
          autoFocus,
          autoComplete,
          maxLength,
          'aria-invalid': fieldState.error ? true : undefined,
        };

        return (
          <FieldShell
            label={label}
            htmlFor={id}
            required={required}
            error={fieldState.error?.message}
            helperText={helperText}
            fullWidth={fullWidth}
            className={className}
          >
            {multiline ? (
              <textarea
                {...sharedProps}
                ref={field.ref}
                rows={rows}
                onChange={(event) => handleChange(event.target.value)}
                className={cn(CONTROL_CLASSES, 'px-3 py-2', fieldState.error && ERROR_CLASSES)}
              />
            ) : (
              <div className="relative flex items-center">
                {startAdornment && (
                  <span className="pointer-events-none absolute left-3 text-sm text-ink-muted">
                    {startAdornment}
                  </span>
                )}
                <input
                  {...sharedProps}
                  ref={field.ref}
                  type={type}
                  min={min}
                  max={max}
                  step={step}
                  pattern={pattern}
                  onChange={(event) => handleChange(event.target.value)}
                  className={cn(
                    CONTROL_CLASSES,
                    'h-10 px-3',
                    startAdornment && 'pl-9',
                    endAdornment && 'pr-9',
                    fieldState.error && ERROR_CLASSES,
                  )}
                />
                {endAdornment && (
                  <span className="absolute right-3 text-sm text-ink-muted">{endAdornment}</span>
                )}
              </div>
            )}
          </FieldShell>
        );
      }}
    />
  );
};

export default Input;
