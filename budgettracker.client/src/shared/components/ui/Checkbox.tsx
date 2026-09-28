import { useId } from 'react';
import * as RadixCheckbox from '@radix-ui/react-checkbox';
import { Check } from 'lucide-react';
import { Controller } from 'react-hook-form';
import type { Control, FieldPath, FieldValues } from 'react-hook-form';
import { cn } from '../../utils/cn';

export interface CheckboxProps<TFieldValues extends FieldValues> {
  control: Control<TFieldValues>;
  name: FieldPath<TFieldValues>;
  label: string;
  disabled?: boolean;
  className?: string;
}

/**
 * Form checkbox with label (BUD-20). Replaces MUI Checkbox+FormControlLabel
 * and absorbs the Controller block callers used to write by hand.
 */
const Checkbox = <TFieldValues extends FieldValues>({
  control,
  name,
  label,
  disabled = false,
  className,
}: CheckboxProps<TFieldValues>) => {
  const id = useId();

  return (
    <Controller
      control={control}
      name={name}
      render={({ field }) => (
        <div className={cn('flex items-center gap-2.5', className)}>
          <RadixCheckbox.Root
            id={id}
            ref={field.ref}
            checked={Boolean(field.value)}
            // Radix yields boolean | 'indeterminate'; Zod expects a boolean.
            onCheckedChange={(checked) => field.onChange(checked === true)}
            onBlur={field.onBlur}
            disabled={disabled}
            className={cn(
              'focus-ring flex h-[18px] w-[18px] shrink-0 items-center justify-center rounded-sm border border-border-strong bg-surface shadow-xs transition-colors duration-120',
              'data-[state=checked]:border-primary data-[state=checked]:bg-primary data-[state=checked]:text-white',
              'disabled:cursor-not-allowed disabled:opacity-50',
            )}
          >
            <RadixCheckbox.Indicator>
              <Check size={13} strokeWidth={3} />
            </RadixCheckbox.Indicator>
          </RadixCheckbox.Root>
          <label htmlFor={id} className="cursor-pointer select-none text-sm text-ink">
            {label}
          </label>
        </div>
      )}
    />
  );
};

export default Checkbox;
