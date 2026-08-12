import MenuItem from '@mui/material/MenuItem';
import type { FieldValues } from 'react-hook-form';
import BudInput from './BudInput';
import type { BudInputProps } from './BudInput';

export interface BudSelectOption<TValue extends string | number = string> {
  readonly value: TValue;
  readonly label: string;
  readonly disabled?: boolean;
}

export interface BudSelectProps<
  TFieldValues extends FieldValues,
  TValue extends string | number = string,
> extends Omit<
    BudInputProps<TFieldValues>,
    | 'type'
    | 'multiline'
    | 'rows'
    | 'min'
    | 'max'
    | 'step'
    | 'startAdornment'
    | 'endAdornment'
    | 'shrinkLabel'
    | 'placeholder'
    | 'select'
    | 'children'
  > {
  options: readonly BudSelectOption<TValue>[];
  /** Adds a leading blank choice, e.g. "Uncategorized". */
  emptyOptionLabel?: string;
}

/**
 * Select field (BUD-16). Implemented on BudInput's `select` mode so both share
 * one Controller/validation code path.
 */
const BudSelect = <TFieldValues extends FieldValues, TValue extends string | number = string>({
  options,
  emptyOptionLabel,
  ...rest
}: BudSelectProps<TFieldValues, TValue>) => (
  <BudInput<TFieldValues> {...rest} select>
    {emptyOptionLabel !== undefined && <MenuItem value="">{emptyOptionLabel}</MenuItem>}
    {options.map((option) => (
      <MenuItem key={String(option.value)} value={option.value} disabled={option.disabled}>
        {option.label}
      </MenuItem>
    ))}
  </BudInput>
);

export default BudSelect;
