import { useMemo, useState } from 'react';
import { Alert, Button, Card, ConfirmModal, Skeleton } from '../../../shared/components/ui';
import type { CategoryRule } from '../../../shared/types/api';
import type { CategoryRuleFormInput } from '../../../shared/validation/categoryRuleSchema';
import { errorMessage } from '../../../shared/utils/errorMessage';
import { useCategories } from '../hooks/useCategories';
import { useCategoryRuleMutations, useCategoryRules } from '../hooks/useCategoryRules';
import CategoryRuleDialog from './CategoryRuleDialog';

const describeMatch = (rule: CategoryRule) =>
  `${rule.matchType === 'Exact' ? 'Payee is exactly' : 'Payee contains'} “${rule.pattern}”`;

const matchedLabel = (n: number) => `${n} transaction${n === 1 ? '' : 's'} filed`;

/** Settings → Rules (BUD-24): every payee rule, with edit and delete. Rules are created from a transaction's row. */
const CategoryRulesSection = () => {
  const rules = useCategoryRules();
  const categories = useCategories();
  const { remove } = useCategoryRuleMutations();
  const [editing, setEditing] = useState<CategoryRule | null>(null);
  const [deleting, setDeleting] = useState<CategoryRule | null>(null);
  const [status, setStatus] = useState<string | null>(null);

  const editValues = useMemo<CategoryRuleFormInput>(
    () =>
      editing
        ? { pattern: editing.pattern, matchType: editing.matchType, categoryId: editing.categoryId, applyToExisting: false }
        : { pattern: '', matchType: 'Contains', categoryId: 0, applyToExisting: false },
    [editing],
  );

  return (
    <Card title="Rules" subtitle="Payees that always land in the same category. New imports follow them.">
      <div className="space-y-3">
        {status && <Alert severity="success" message={status} onClose={() => setStatus(null)} />}
        {remove.error && (
          <Alert severity="error" message={errorMessage(remove.error, "Couldn't delete the rule. Please try again.")} />
        )}
        {rules.isPending && <Skeleton className="h-16 w-full" />}
        {rules.isError && <Alert severity="error" message="Couldn't load your rules." />}
        {rules.data?.length === 0 && (
          <p className="text-sm text-ink-muted">
            No rules yet. On a transaction, choose “Always categorize…” to file that payee automatically.
          </p>
        )}
        {rules.data && rules.data.length > 0 && (
          <ul className="divide-y divide-border-subtle">
            {rules.data.map((rule) => (
              <li key={rule.id} className="flex flex-wrap items-center justify-between gap-3 py-2.5">
                <div className="min-w-0">
                  <p className="truncate text-sm text-ink">
                    {describeMatch(rule)} → <span className="font-medium">{rule.categoryName}</span>
                  </p>
                  <p className="text-xs text-ink-muted">{matchedLabel(rule.matchedCount)}</p>
                </div>
                <div className="flex shrink-0 gap-2">
                  <Button
                    size="sm"
                    variant="secondary"
                    aria-label={`Edit rule ${rule.pattern}`}
                    onClick={() => setEditing(rule)}
                  >
                    Edit
                  </Button>
                  <Button
                    size="sm"
                    variant="destructive-ghost"
                    aria-label={`Delete rule ${rule.pattern}`}
                    onClick={() => setDeleting(rule)}
                  >
                    Delete
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <CategoryRuleDialog
        open={editing !== null}
        ruleId={editing?.id}
        initialValues={editValues}
        categories={categories.data ?? []}
        onClose={() => setEditing(null)}
        onSaved={setStatus}
      />

      <ConfirmModal
        open={deleting !== null}
        title="Delete rule?"
        message="Transactions it already filed keep their category. New imports stop following it."
        confirmLabel="Delete rule"
        destructive
        isPending={remove.isPending}
        onCancel={() => setDeleting(null)}
        onConfirm={() => {
          if (!deleting) return;
          const rule = deleting;
          setDeleting(null);
          remove.mutate(rule.id, { onSuccess: () => setStatus('Rule deleted.') });
        }}
      />
    </Card>
  );
};

export default CategoryRulesSection;
