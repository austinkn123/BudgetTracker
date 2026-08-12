import type { ReactNode } from 'react';
import { Controller } from 'react-hook-form';
import type { Control, FieldPath, FieldValues } from 'react-hook-form';
import TextField from '@mui/material/TextField';
import InputAdornment from '@mui/material/InputAdornment';
import type { SxProps, Theme } from '@mui/material/styles';

export type BudInputValueAs = 'string' | 'number';

export type BudInputType =
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

/** Types whose label must shrink up front or it overlaps the native picker text. */
const SHRINK_TYPES: readonly BudInputType[] = ['date', 'month', 'time', 'datetime-local'];

export interface BudInputProps<TFieldValues extends FieldValues> {
  control: Control<TFieldValues>;
  name: FieldPath<TFieldValues>;
  label: string;
  type?: BudInputType;
  /** 'number' coerces on change: empty -> undefined, otherwise Number(value). */
  valueAs?: BudInputValueAs;
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
  shrinkLabel?: boolean;
  sx?: SxProps<Theme>;
  /** Escape hatch for `select` mode — used by BudSelect, not by feature code. */
  select?: boolean;
  children?: ReactNode;
}

/**
 * Form text field (BUD-16).
 *
 * Wraps React Hook Form's Controller internally, so call sites drop the ~14-line
 * Controller/TextField block and never wire `error`/`helperText` by hand.
 */
const BudInput = <TFieldValues extends FieldValues>({
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
  rows,
  min,
  max,
  step,
  maxLength,
  pattern,
  startAdornment,
  endAdornment,
  shrinkLabel,
  sx,
  select = false,
  children,
}: BudInputProps<TFieldValues>) => {
  const shouldShrink = shrinkLabel ?? (SHRINK_TYPES.includes(type) || undefined);

  const htmlInput: Record<string, unknown> = {};
  if (min !== undefined) htmlInput.min = min;
  if (max !== undefined) htmlInput.max = max;
  if (step !== undefined) htmlInput.step = step;
  if (maxLength !== undefined) htmlInput.maxLength = maxLength;
  if (pattern !== undefined) htmlInput.pattern = pattern;

  return (
    <Controller
      control={control}
      name={name}
      render={({ field, fieldState }) => (
        <TextField
          {...field}
          value={field.value ?? ''}
          onChange={(event) => {
            if (valueAs === 'number') {
              const raw = event.target.value;
              field.onChange(raw === '' ? undefined : Number(raw));
              return;
            }
            field.onChange(event.target.value);
          }}
          label={label}
          type={select ? undefined : type}
          select={select}
          placeholder={placeholder}
          required={required}
          disabled={disabled}
          fullWidth={fullWidth}
          autoFocus={autoFocus}
          autoComplete={autoComplete}
          multiline={multiline}
          rows={rows}
          error={Boolean(fieldState.error)}
          helperText={fieldState.error?.message ?? helperText}
          slotProps={{
            ...(Object.keys(htmlInput).length > 0 && { htmlInput }),
            ...(shouldShrink !== undefined && { inputLabel: { shrink: shouldShrink } }),
            ...((startAdornment || endAdornment) && {
              input: {
                ...(startAdornment && {
                  startAdornment: <InputAdornment position="start">{startAdornment}</InputAdornment>,
                }),
                ...(endAdornment && {
                  endAdornment: <InputAdornment position="end">{endAdornment}</InputAdornment>,
                }),
              },
            }),
          }}
          sx={sx}
        >
          {children}
        </TextField>
      )}
    />
  );
};

export default BudInput;
