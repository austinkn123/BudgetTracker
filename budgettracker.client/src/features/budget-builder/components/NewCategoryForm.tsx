import { useId } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { categorySchema } from '../../../shared/validation/categorySchema';

const nameSchema = categorySchema.pick({ name: true });

interface NewCategoryFormProps {
  /** Lets the dialog's submit button submit it via `form={id}`. */
  id: string;
  /** A server error, such as a name already in use. */
  error?: string | null;
  onSubmit: (name: string) => void;
}

/** One big name field, for adding something the category list doesn't have yet. */
const NewCategoryForm = ({ id, error, onSubmit }: NewCategoryFormProps) => {
  const inputId = useId();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({ resolver: zodResolver(nameSchema), defaultValues: { name: '' } });
  const message = errors.name?.message ?? error;

  return (
    <form id={id} noValidate onSubmit={handleSubmit(({ name }) => onSubmit(name))} className="flex flex-col gap-3">
      <label htmlFor={inputId} className="text-lg font-semibold text-ink">
        What do you call it?
      </label>
      {/* Native <input>: register()-bound hero name field with its own big label; Input is Controller-bound with the small FieldShell label. */}
      <input
        id={inputId}
        type="text"
        autoComplete="off"
        maxLength={100}
        placeholder="For example: Pet food"
        aria-invalid={message ? true : undefined}
        {...register('name')}
        className="focus-ring h-16 w-full rounded-2xl border-2 border-border-strong bg-surface px-4 text-2xl font-semibold text-ink placeholder:font-normal placeholder:text-ink-muted/60 focus:border-primary aria-[invalid]:border-error"
      />
      {message && (
        <p role="alert" className="text-base font-medium text-error">
          {message}
        </p>
      )}
      <p className="text-base text-ink-muted">Next, you can set how much it is.</p>
    </form>
  );
};

export default NewCategoryForm;
