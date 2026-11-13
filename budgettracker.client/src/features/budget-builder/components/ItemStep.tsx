import { useState } from 'react';
import type { ReactNode } from 'react';
import { Check, Plus } from 'lucide-react';
import { Button, Modal } from '../../../shared/components/ui';
import type { Category } from '../../../shared/types/api';
import { cn } from '../../../shared/utils/cn';
import { errorMessage } from '../../../shared/utils/errorMessage';
import { currency, currencyPrecise } from '../../../shared/utils/format';
import { useCategoryMutations } from '../../categories/hooks/useCategoryMutations';
import { newItemKey, type Item, type StepKey } from '../utils/budgetBuilder';
import AmountForm from './AmountForm';
import NewCategoryForm from './NewCategoryForm';

const COPY: Record<StepKey, { title: string; lead: string; add: string; ask: string }> = {
  bills: {
    title: 'Which bills do you pay every month?',
    lead: 'Tap a bill to add or remove it. Tap the amount to change it.',
    add: 'Add another bill',
    ask: 'How much each month?',
  },
  everyday: {
    title: 'How much do you spend on everyday things?',
    lead: 'These change each month. The bank amount is your monthly average over the last year.',
    add: 'Add something else',
    ask: 'How much each month?',
  },
  yearly: {
    title: 'Any bills you pay once a year?',
    lead: 'Type the amount for the whole year. We set aside a little each month, so the big bill is not a surprise.',
    add: 'Add a yearly bill',
    ask: 'How much each year?',
  },
};

const BIG_BUTTON = 'h-14 rounded-xl text-lg';
const AMOUNT_FORM_ID = 'item-amount';
const NEW_CATEGORY_FORM_ID = 'new-category';

/** The builder shows its own errors; the shared category hook's status messages have nowhere to go here. */
const ignore = () => {};

/** Whole dollars unless the amount has cents: a $45.99 bill should not read as $46. */
const money = (amount: number) => (Number.isInteger(amount) ? currency : currencyPrecise).format(amount);

const hintFor = (item: Item, step: StepKey) =>
  step === 'yearly'
    ? `= ${currency.format(item.amount / 12)} a month`
    : item.bankMonthly > 0
      ? `Bank: about ${currency.format(item.bankMonthly)}`
      : 'Bank: none found';

interface ItemStepProps {
  step: StepKey;
  items: Item[];
  /** Categories that can still be added here, with their bank averages. */
  addable: { category: Category; bankMonthly: number }[];
  onChange: (items: Item[]) => void;
  onAdd: (category: Category, bankMonthly: number) => void;
  /** Shown between the question and the list. */
  note?: ReactNode;
}

/** One builder step: a checklist of items, each with an amount to tap and change. */
const ItemStep = ({ step, items, addable, onChange, onAdd, note }: ItemStepProps) => {
  const copy = COPY[step];
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const [adding, setAdding] = useState<'closed' | 'pick' | 'new'>('closed');
  const editing = items.find((i) => i.key === editingKey) ?? null;
  const { createCategory } = useCategoryMutations({ setStatusMessage: ignore, setStatusError: ignore });

  const update = (key: string, changes: Partial<Item>) =>
    onChange(items.map((i) => (i.key === key ? { ...i, ...changes } : i)));

  // Adding asks "how much?" straight away, so a new item never sits at an amount nobody chose.
  const pick = (category: Category, bankMonthly: number) => {
    onAdd(category, bankMonthly);
    setAdding('closed');
    setEditingKey(newItemKey(step, category.id));
  };

  // A name that matches a category on the list picks that one rather than failing as a duplicate.
  const addNew = (name: string) => {
    const match = addable.find((a) => a.category.name.toLowerCase() === name.toLowerCase());
    if (match) return pick(match.category, match.bankMonthly);
    createCategory.mutate(
      { userId: 0, name, categoryType: 'Expense' },
      { onSuccess: (created) => pick(created, 0) },
    );
  };

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h2 className="text-3xl font-semibold leading-tight tracking-[-0.025em] text-ink">{copy.title}</h2>
        <p className="text-lg text-ink-muted">{copy.lead}</p>
      </div>

      {note}

      <ul className="flex flex-col gap-3">
        {items.map((item) => (
          <li
            key={item.key}
            className={cn('rounded-2xl border-[1.5px] border-border p-4', item.on ? 'bg-surface' : 'bg-background')}
          >
            {/* Native <button>: full-row role="checkbox" toggle with a 32px tick; neither Button nor InlineCheckbox covers a whole-row target. */}
            <button
              type="button"
              role="checkbox"
              aria-checked={item.on}
              onClick={() => update(item.key, { on: !item.on })}
              className="focus-ring flex min-h-12 w-full items-center gap-4 rounded-lg text-left"
            >
              <span
                className={cn(
                  'grid size-8 shrink-0 place-items-center rounded-lg',
                  item.on ? 'bg-primary text-white' : 'border-2 border-border-strong bg-surface',
                )}
              >
                {item.on && <Check size={22} strokeWidth={3} aria-hidden />}
              </span>
              <span className={cn('text-lg font-semibold', item.on ? 'text-ink' : 'text-ink-muted')}>{item.name}</span>
            </button>
            <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 pl-12">
              {/* Native <button>: oversized amount chip (text-xl, min-h-12) that reads as the figure itself, not as a Button. */}
              <button
                type="button"
                onClick={() => setEditingKey(item.key)}
                aria-label={`Change the amount for ${item.name}`}
                className={cn(
                  'focus-ring numeric min-h-12 rounded-xl border-[1.5px] border-border-strong px-4 text-xl font-semibold transition-colors hover:border-primary',
                  item.on ? 'bg-background text-ink' : 'bg-surface text-ink-muted',
                )}
              >
                {money(item.amount)}
                {step === 'yearly' && ' a year'}
              </button>
              <span className="text-base text-ink-muted">{hintFor(item, step)}</span>
            </div>
          </li>
        ))}
      </ul>

      {/* Native <button>: dashed full-width "add" tile; no Button variant draws a dashed placeholder. */}
      <button
        type="button"
        onClick={() => setAdding('pick')}
        className="focus-ring flex min-h-16 w-full items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-border-strong text-lg font-semibold text-primary transition-colors hover:bg-primary-subtle"
      >
        <Plus size={24} aria-hidden />
        {copy.add}
      </button>

      <Modal
        open={editing !== null}
        onClose={() => setEditingKey(null)}
        title={editing?.name ?? ''}
        maxWidth="xs"
        actions={
          <div className="flex w-full flex-col gap-2">
            <Button type="submit" form={AMOUNT_FORM_ID} size="lg" fullWidth className={BIG_BUTTON}>
              Save
            </Button>
            <Button variant="secondary" size="lg" fullWidth className={BIG_BUTTON} onClick={() => setEditingKey(null)}>
              Cancel
            </Button>
            {editing?.on && (
              <Button
                variant="ghost"
                size="lg"
                fullWidth
                className={BIG_BUTTON}
                onClick={() => {
                  update(editing.key, { on: false });
                  setEditingKey(null);
                }}
              >
                Remove from my budget
              </Button>
            )}
          </div>
        }
      >
        {editing && (
          <AmountForm
            key={editing.key}
            id={AMOUNT_FORM_ID}
            label={copy.ask}
            defaultAmount={editing.amount}
            step={step === 'yearly' ? 50 : 10}
            onSubmit={(amount) => {
              update(editing.key, { amount, on: true });
              setEditingKey(null);
            }}
          >
            {editing.bankMonthly > 0 && (
              <p className="rounded-xl bg-primary-subtle p-4 text-lg text-ink">
                Your bank: about <span className="numeric font-semibold">{currency.format(editing.bankMonthly)}</span> a
                month.
              </p>
            )}
          </AmountForm>
        )}
      </Modal>

      <Modal
        open={adding !== 'closed'}
        onClose={() => setAdding('closed')}
        title={adding === 'new' ? 'Add something new' : copy.add}
        maxWidth="xs"
        disableBackdropClose={createCategory.isPending}
        actions={
          adding === 'new' ? (
            <div className="flex w-full flex-col gap-2">
              <Button
                type="submit"
                form={NEW_CATEGORY_FORM_ID}
                size="lg"
                fullWidth
                className={BIG_BUTTON}
                loading={createCategory.isPending}
              >
                Next
              </Button>
              <Button variant="secondary" size="lg" fullWidth className={BIG_BUTTON} onClick={() => setAdding('pick')}>
                Back to the list
              </Button>
            </div>
          ) : (
            <Button variant="secondary" size="lg" fullWidth className={BIG_BUTTON} onClick={() => setAdding('closed')}>
              Cancel
            </Button>
          )
        }
      >
        {adding === 'new' ? (
          <NewCategoryForm
            id={NEW_CATEGORY_FORM_ID}
            error={createCategory.error && errorMessage(createCategory.error, "Couldn't add it. Please try again.")}
            onSubmit={addNew}
          />
        ) : (
          <>
            {/* Native <button>: dashed full-width "make a new one" tile; no Button variant draws a dashed placeholder. */}
            <button
              type="button"
              onClick={() => {
                createCategory.reset();
                setAdding('new');
              }}
              className="focus-ring flex min-h-14 w-full items-center justify-center gap-2 rounded-xl border-2 border-dashed border-border-strong text-lg font-semibold text-primary transition-colors hover:bg-primary-subtle"
            >
              <Plus size={22} aria-hidden />
              Not on the list? Make a new one
            </button>
            {addable.length > 0 && (
              <ul className="flex flex-col gap-2">
                {addable.map(({ category, bankMonthly }) => (
                  <li key={category.id}>
                    {/* Native <button>: full-row, two-line pick target; Button forces a single nowrap line. */}
                    <button
                      type="button"
                      onClick={() => pick(category, bankMonthly)}
                      className="focus-ring flex min-h-14 w-full flex-col items-start justify-center rounded-xl border-[1.5px] border-border px-4 py-3 text-left transition-colors hover:border-primary hover:bg-primary-subtle"
                    >
                      <span className="text-lg font-semibold text-ink">{category.name}</span>
                      {bankMonthly > 0 && (
                        <span className="numeric text-base text-ink-muted">
                          about {currency.format(bankMonthly)} a month
                        </span>
                      )}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </Modal>
    </div>
  );
};

export default ItemStep;
