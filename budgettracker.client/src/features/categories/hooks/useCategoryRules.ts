import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { categoryRuleService } from '../../../shared/services/categoryRule.service';
import type { CategoryRule, CategoryRuleRequest } from '../../../shared/types/api';

export const categoryRulesKey = ['category-rules'] as const;

/** The user's payee rules (BUD-24), oldest first. */
export const useCategoryRules = () =>
  useQuery<CategoryRule[], Error>({
    queryKey: categoryRulesKey,
    queryFn: () => categoryRuleService.getAll(),
    retry: 1,
  });

/** `value`, once it has stopped changing for `delayMs`. */
const useSettled = <T,>(value: T, delayMs: number) => {
  const [settled, setSettled] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setSettled(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);
  return settled;
};

/**
 * How many uncategorized rows `rule` would file if created now. Null `rule` (an invalid form) asks nothing; typing
 * settles for 300 ms before asking, so a pattern doesn't cost one request per keystroke.
 */
export const useCategoryRulePreview = (rule: CategoryRuleRequest | null) => {
  const settled = useSettled(rule, 300);
  return useQuery<number, Error>({
    queryKey: [...categoryRulesKey, 'preview', settled],
    queryFn: () => categoryRuleService.preview(settled!),
    enabled: settled !== null,
    placeholderData: (previous) => previous,
    retry: false,
  });
};

/** Create, edit and delete payee rules, refreshing what each change can move. */
export const useCategoryRuleMutations = () => {
  const queryClient = useQueryClient();
  const invalidate = (keys: string[]) =>
    Promise.all(keys.map((key) => queryClient.invalidateQueries({ queryKey: [key] })));
  // Filing existing rows moves the ledger, the summary's uncategorized count, and every figure summed by category.
  const ledgerKeys = ['transactions', 'budget-analysis', 'cash-flow', 'money-map'];

  const create = useMutation({
    mutationFn: (rule: CategoryRuleRequest) => categoryRuleService.create(rule),
    onSuccess: (created) =>
      invalidate(created.appliedCount > 0 ? [categoryRulesKey[0], ...ledgerKeys] : [categoryRulesKey[0]]),
  });

  const update = useMutation({
    mutationFn: ({ id, rule }: { id: number; rule: CategoryRuleRequest }) => categoryRuleService.update(id, rule),
    onSuccess: () => invalidate([categoryRulesKey[0]]),
  });

  const remove = useMutation({
    mutationFn: (id: number) => categoryRuleService.delete(id),
    onSuccess: () => invalidate([categoryRulesKey[0]]),
  });

  return { create, update, remove };
};
