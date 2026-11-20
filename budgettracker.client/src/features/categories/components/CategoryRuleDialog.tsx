import { useEffect, useMemo } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Checkbox, Input, Modal, ModalActions, Select } from '../../../shared/components/ui';
import type { Category, CategoryRuleRequest } from '../../../shared/types/api';
import {
  categoryRuleSchema,
  type CategoryRuleFormInput,
  type CategoryRuleFormValues,
} from '../../../shared/validation/categoryRuleSchema';
import { errorMessage } from '../../../shared/utils/errorMessage';
import { useCategoryRuleMutations, useCategoryRulePreview } from '../hooks/useCategoryRules';

const MATCH_TYPE_OPTIONS = [
  { value: 'Contains', label: 'Payee contains' },
  { value: 'Exact', label: 'Payee is exactly' },
] as const;

type CategoryRuleDialogProps = {
  open: boolean;
  /** Absent: create a rule. Present: edit that rule. */
  ruleId?: number;
  initialValues: CategoryRuleFormInput;
  categories: Category[];
  onClose: () => void;
  /** Called with a one-line summary once the rule is saved. */
  onSaved?: (message: string) => void;
};

const plural = (n: number) => `${n} existing uncategorized transaction${n === 1 ? '' : 's'}`;

/**
 * Create or edit an "always categorize this payee as" rule (BUD-24). On create, offers to file the uncategorized rows
 * the rule would win, with a live count.
 */
const CategoryRuleDialog = ({ open, ruleId, initialValues, categories, onClose, onSaved }: CategoryRuleDialogProps) => {
  const isEdit = ruleId !== undefined;
  const { create, update } = useCategoryRuleMutations();
  const mutation = isEdit ? update : create;

  const { control, handleSubmit, reset } = useForm<CategoryRuleFormInput, unknown, CategoryRuleFormValues>({
    resolver: zodResolver(categoryRuleSchema),
    defaultValues: initialValues,
  });

  useEffect(() => {
    if (open) {
      reset(initialValues);
      create.reset();
      update.reset();
    }
    // The mutation objects change identity every render; resetting on open is what matters.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, initialValues, reset]);

  const [pattern, matchType, categoryId] = useWatch({ control, name: ['pattern', 'matchType', 'categoryId'] });
  const previewRequest = useMemo<CategoryRuleRequest | null>(() => {
    if (isEdit || !open) return null;
    const parsed = categoryRuleSchema.safeParse({ pattern, matchType, categoryId });
    return parsed.success ? { pattern: parsed.data.pattern, matchType: parsed.data.matchType, categoryId: parsed.data.categoryId } : null;
  }, [isEdit, open, pattern, matchType, categoryId]);
  const preview = useCategoryRulePreview(previewRequest);

  const categoryOptions = useMemo(() => categories.map((c) => ({ value: c.id, label: c.name })), [categories]);

  const submit = handleSubmit(async (values) => {
    const rule: CategoryRuleRequest = { pattern: values.pattern, matchType: values.matchType, categoryId: values.categoryId };
    if (isEdit) {
      await update.mutateAsync({ id: ruleId, rule });
      onSaved?.('Rule saved.');
    } else {
      const created = await create.mutateAsync({ ...rule, applyToExisting: values.applyToExisting });
      const n = created.appliedCount;
      onSaved?.(n > 0 ? `Rule saved. Filed ${n} transaction${n === 1 ? '' : 's'}.` : 'Rule saved.');
    }
    onClose();
  });

  const applyLabel =
    previewRequest === null
      ? 'Also apply to existing uncategorized transactions'
      : preview.data === undefined
        ? 'Also apply to existing uncategorized transactions (counting…)'
        : `Also apply to ${plural(preview.data)}`;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={isEdit ? 'Edit rule' : 'Always categorize as'}
      maxWidth="sm"
      disableBackdropClose={mutation.isPending}
      actions={
        <ModalActions
          onCancel={onClose}
          onConfirm={() => void submit().catch(() => undefined)}
          confirmLabel={isEdit ? 'Save changes' : 'Create rule'}
          isPending={mutation.isPending}
        />
      }
    >
      <div className="space-y-4">
        {mutation.error && (
          <Alert severity="error" message={errorMessage(mutation.error, "Couldn't save the rule. Please try again.")} />
        )}
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Select control={control} name="matchType" label="Match" options={MATCH_TYPE_OPTIONS} required />
          <Input
            control={control}
            name="pattern"
            label="Payee text"
            helperText="Ignores case. Shorten it to catch variants, e.g. “uber”."
            required
            autoFocus
          />
        </div>
        <Select
          control={control}
          name="categoryId"
          label="Category"
          options={categoryOptions}
          valueAs="number"
          placeholder="Pick a category"
          required
        />
        {!isEdit && <Checkbox control={control} name="applyToExisting" label={applyLabel} />}
        <p className="text-xs text-ink-muted">
          New imports matching this rule get its category. A category you set by hand is never changed.
        </p>
      </div>
    </Modal>
  );
};

export default CategoryRuleDialog;
