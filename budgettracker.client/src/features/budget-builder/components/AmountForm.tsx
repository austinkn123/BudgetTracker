import { useId } from 'react';
import type { ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Minus, Plus } from 'lucide-react';
import { amountSchema, type AmountFormData } from '../../../shared/validation/amountSchema';

interface AmountFormProps {
  /** Lets a submit button outside the form (a dialog's actions, the page footer) submit it via `form={id}`. */
  id: string;
  label: string;
  defaultAmount: number;
  /** How far − and + move the amount. */
  step: number;
  onSubmit: (amount: number) => void;
  children?: ReactNode;
}

const NUDGE_BUTTON =
  'focus-ring grid size-16 shrink-0 place-items-center rounded-full border-[1.5px] border-border-strong bg-surface text-ink transition-colors hover:bg-background';

const toCents = (amount: number) => Math.round(amount * 100) / 100;

/** One big money field with − and + buttons, so an amount can be changed without typing. */
const AmountForm = ({ id, label, defaultAmount, step, onSubmit, children }: AmountFormProps) => {
  const inputId = useId();
  const {
    register,
    handleSubmit,
    getValues,
    setValue,
    formState: { errors },
  } = useForm<AmountFormData>({
    resolver: zodResolver(amountSchema),
    defaultValues: { amount: defaultAmount },
  });

  const nudge = (delta: number) => {
    const current = getValues('amount');
    const next = (Number.isFinite(current) ? current : 0) + delta;
    setValue('amount', Math.max(0, toCents(next)), { shouldValidate: true });
  };

  return (
    <form id={id} noValidate onSubmit={handleSubmit(({ amount }) => onSubmit(amount))} className="flex flex-col gap-3">
      <label htmlFor={inputId} className="text-lg font-semibold text-ink">
        {label}
      </label>
      <div className="flex items-center gap-3">
        {/* Native <button>: 64px round nudge control sized for the large-target builder; Button sizes cap at h-11. */}
        <button type="button" onClick={() => nudge(-step)} aria-label={`$${step} less`} className={NUDGE_BUTTON}>
          <Minus size={28} aria-hidden />
        </button>
        <div className="relative min-w-0 flex-1">
          <span
            aria-hidden
            className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-3xl font-bold text-ink-muted"
          >
            $
          </span>
          {/* Native <input>: register()-bound hero amount field with its own big label; Input is Controller-bound with the small FieldShell label. */}
          <input
            id={inputId}
            type="number"
            inputMode="decimal"
            min={0}
            step="0.01"
            aria-invalid={errors.amount ? true : undefined}
            {...register('amount', { valueAsNumber: true })}
            className="focus-ring numeric h-20 w-full rounded-2xl border-2 border-border-strong bg-surface pl-10 pr-3 text-4xl font-bold tracking-[-0.03em] text-ink [appearance:textfield] focus:border-primary aria-[invalid]:border-error [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
          />
        </div>
        {/* Native <button>: 64px round nudge control, paired with the one above. */}
        <button type="button" onClick={() => nudge(step)} aria-label={`$${step} more`} className={NUDGE_BUTTON}>
          <Plus size={28} aria-hidden />
        </button>
      </div>
      {errors.amount && (
        <p role="alert" className="text-base font-medium text-error">
          {errors.amount.message}
        </p>
      )}
      {children}
    </form>
  );
};

export default AmountForm;
