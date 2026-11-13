import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Check, ChevronLeft, Landmark, Wallet } from 'lucide-react';
import { Alert, Button, Skeleton } from '../../../shared/components/ui';
import { budgetPlanService } from '../../../shared/services/budgetPlan.service';
import type { BudgetPlan, Category, MoneyMap } from '../../../shared/types/api';
import { cn } from '../../../shared/utils/cn';
import { errorMessage } from '../../../shared/utils/errorMessage';
import { currency } from '../../../shared/utils/format';
import { invalidatePlanQueries, useBudgetPlans } from '../../budget-plans/hooks/useBudgetPlans';
import { useCategories } from '../../categories/hooks/useCategories';
import { useMoneyMap } from '../../money-map/hooks/useMoneyMap';
import AmountForm from '../components/AmountForm';
import ItemStep from '../components/ItemStep';
import {
  addableCategories,
  addItem,
  buildDraft,
  clearSavedDraft,
  ITEM_STEPS,
  pickPlan,
  planStamp,
  readSavedDraft,
  RESUMABLE_SCREENS,
  toPlan,
  totals,
  writeSavedDraft,
  type StepKey,
} from '../utils/budgetBuilder';

const SCREENS = ['start', ...RESUMABLE_SCREENS, 'done'] as const;
type Screen = (typeof SCREENS)[number];

/** The four question screens, numbered for "Step 2 of 4". */
const QUESTIONS: readonly Screen[] = ['income', 'bills', 'everyday', 'yearly'];

const QUESTION_LABELS = ['Money coming in', 'Bills you must pay', 'Everyday spending', 'Bills once a year'];

/** Same window as the money map's default, so the averages match what it shows and the query is shared. */
const AVERAGE_MONTHS = 12;

const BIG_BUTTON = 'h-14 rounded-xl text-lg';
const INCOME_FORM_ID = 'income-amount';

const isItemStep = (screen: Screen): screen is StepKey => (ITEM_STEPS as readonly string[]).includes(screen);

const StepHeader = ({ screen, onBack }: { screen: Screen; onBack: () => void }) => {
  const number = QUESTIONS.indexOf(screen) + 1;
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        {/* Native <button>: primary-coloured text "Back" link at the builder's large text-lg size; no Button variant is a bare primary text link. */}
        <button
          type="button"
          onClick={onBack}
          className="focus-ring -ml-2 flex min-h-12 items-center gap-1 rounded-lg px-2 text-lg font-semibold text-primary"
        >
          <ChevronLeft size={26} aria-hidden />
          Back
        </button>
        <span className="text-base font-semibold text-ink-muted">
          {number > 0 ? `Step ${number} of ${QUESTIONS.length}` : 'Last step'}
        </span>
      </div>
      {number > 0 && (
        <div className="flex gap-1.5" aria-hidden>
          {QUESTIONS.map((q, i) => (
            <span key={q} className={cn('h-2 flex-1 rounded-full', i < number ? 'bg-primary' : 'bg-border')} />
          ))}
        </div>
      )}
    </div>
  );
};

/** Pinned to the bottom of the screen: what is left so far, and the one button that moves on. */
const Footer = ({ left, children }: { left?: number; children: ReactNode }) => (
  <div className="sticky bottom-4 z-10 mt-8 flex flex-col gap-3 rounded-2xl border border-border bg-surface p-4 shadow-lg">
    {left !== undefined && (
      <div className="flex items-center justify-between text-lg font-medium text-ink-muted" aria-live="polite">
        <span>{left < 0 ? 'Over by' : 'Money left'}</span>
        <span className={cn('numeric text-2xl font-bold', left < 0 ? 'text-warning-dark' : 'text-success-dark')}>
          {currency.format(Math.abs(left))}
        </span>
      </div>
    )}
    {children}
  </div>
);

const Title = ({ children }: { children: ReactNode }) => (
  <h2 className="text-3xl font-semibold leading-tight tracking-[-0.025em] text-ink">{children}</h2>
);

interface BuilderProps {
  plan: BudgetPlan | null;
  map: MoneyMap;
  categories: Category[];
}

const Builder = ({ plan, map, categories }: BuilderProps) => {
  const queryClient = useQueryClient();
  const [screen, setScreen] = useState<Screen>('start');
  const [draft, setDraft] = useState(() => buildDraft(plan, map, categories));
  const stamp = planStamp(plan);
  // Answers left part way through this version of the plan, offered on the start screen.
  const [saved, setSaved] = useState(() => readSavedDraft(stamp));
  const sums = totals(draft);

  // Each screen starts at the top, as a new page would.
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [screen]);

  // Keep the answers through a reload or a closed tab. The start screen writes nothing, so an unanswered resume offer
  // isn't overwritten by the fresh draft.
  useEffect(() => {
    if (screen !== 'start' && screen !== 'done') writeSavedDraft(stamp, screen, draft);
  }, [stamp, screen, draft]);

  const move = (by: number) => setScreen(SCREENS[SCREENS.indexOf(screen) + by]);

  const save = useMutation({
    mutationFn: () => {
      const body = toPlan(draft, plan, new Date());
      return plan ? budgetPlanService.update(plan.id, body) : budgetPlanService.create(body);
    },
    onSuccess: async () => {
      await invalidatePlanQueries(queryClient);
      // After the refetch: the new plan's stamp would otherwise re-save the draft we just finished.
      clearSavedDraft();
      setScreen('done');
    },
  });

  const uncategorized = map.lines.find((l) => l.categoryId === null)?.actualMonthly ?? 0;

  if (screen === 'start') {
    return (
      <>
        <div className="space-y-6">
          <span className="grid size-24 place-items-center rounded-full bg-primary-subtle text-primary">
            <Wallet size={48} aria-hidden />
          </span>
          <Title>{plan ? 'Change your monthly budget' : 'Make your monthly budget'}</Title>
          <p className="text-lg text-ink-muted">
            A budget is a plan for your money. We will ask you {QUESTIONS.length} short questions.
          </p>
          <ol className="flex flex-col gap-3">
            {QUESTION_LABELS.map((label, i) => (
              <li
                key={label}
                className="flex items-center gap-4 rounded-2xl border-[1.5px] border-border bg-surface p-4 text-lg font-semibold text-ink"
              >
                <span className="grid size-9 shrink-0 place-items-center rounded-full bg-primary text-white">{i + 1}</span>
                {label}
              </li>
            ))}
          </ol>
          <p className="text-lg text-ink-muted">
            {saved
              ? 'You started this before. Your answers so far are kept.'
              : 'About 5 minutes. Your budget only changes when you save at the end.'}
          </p>
        </div>
        <Footer key={screen}>
          {saved ? (
            <>
              <Button
                size="lg"
                fullWidth
                className={BIG_BUTTON}
                onClick={() => {
                  setDraft(saved.draft);
                  setScreen(saved.screen);
                  setSaved(null);
                }}
              >
                Continue where you left off
              </Button>
              <Button
                variant="secondary"
                size="lg"
                fullWidth
                className={BIG_BUTTON}
                onClick={() => {
                  clearSavedDraft();
                  setSaved(null);
                  move(1);
                }}
              >
                Start over
              </Button>
            </>
          ) : (
            <Button size="lg" fullWidth className={BIG_BUTTON} onClick={() => move(1)}>
              Start
            </Button>
          )}
        </Footer>
      </>
    );
  }

  if (screen === 'income') {
    return (
      <>
        <div className="space-y-6">
          <StepHeader screen={screen} onBack={() => move(-1)} />
          <div className="space-y-2">
            <Title>How much money comes in each month?</Title>
            <p className="text-lg text-ink-muted">Count the money that goes into your bank, after tax.</p>
          </div>
          <AmountForm
            id={INCOME_FORM_ID}
            label="Each month"
            defaultAmount={draft.income}
            step={100}
            onSubmit={(income) => {
              setDraft((d) => ({ ...d, income }));
              move(1);
            }}
          >
            {map.actualIncomeMonthly > 0 && (
              <div className="flex gap-4 rounded-2xl bg-primary-subtle p-4 text-lg text-ink">
                <Landmark size={28} className="shrink-0 text-primary" aria-hidden />
                <p>
                  Your bank: about{' '}
                  <span className="numeric font-semibold">{currency.format(map.actualIncomeMonthly)}</span> came in
                  each month over the last year.
                </p>
              </div>
            )}
          </AmountForm>
        </div>
        <Footer key={screen}>
          <Button type="submit" form={INCOME_FORM_ID} size="lg" fullWidth className={BIG_BUTTON}>
            Next
          </Button>
        </Footer>
      </>
    );
  }

  if (isItemStep(screen)) {
    return (
      <>
        <div className="space-y-6">
          <StepHeader screen={screen} onBack={() => move(-1)} />
          <ItemStep
            key={screen}
            step={screen}
            items={draft[screen]}
            addable={addableCategories(draft, screen, categories, map)}
            onChange={(items) => setDraft((d) => ({ ...d, [screen]: items }))}
            onAdd={(category, bankMonthly) => setDraft((d) => addItem(d, screen, category, bankMonthly))}
            note={
              screen === 'everyday' &&
              uncategorized > 0 && (
                <p className="rounded-2xl bg-warning-subtle p-4 text-lg text-ink">
                  About <span className="numeric font-semibold">{currency.format(uncategorized)}</span> a month of
                  spending is not sorted into a category yet, so these amounts may be low.
                </p>
              )
            }
          />
        </div>
        <Footer key={screen} left={sums.left}>
          <Button size="lg" fullWidth className={BIG_BUTTON} onClick={() => move(1)}>
            Next
          </Button>
        </Footer>
      </>
    );
  }

  if (screen === 'review') {
    const over = sums.left < 0;
    const biggestEveryday = draft.everyday.filter((i) => i.on).sort((a, b) => b.amount - a.amount)[0];
    const segments = [
      { label: 'Bills', value: sums.bills, color: 'bg-primary' },
      { label: 'Everyday', value: sums.everyday, color: 'bg-primary-light' },
      { label: 'Once a year', value: sums.yearly, color: 'bg-border-strong' },
      { label: 'Left over', value: Math.max(sums.left, 0), color: 'bg-success' },
    ].filter((s) => s.value > 0);
    const rows = [
      ['Money coming in', currency.format(draft.income)],
      ['Bills', `− ${currency.format(sums.bills)}`],
      ['Everyday spending', `− ${currency.format(sums.everyday)}`],
      ['Saving for once-a-year bills', `− ${currency.format(sums.yearly)}`],
    ];

    return (
      <>
        <div className="space-y-6">
          <StepHeader screen={screen} onBack={() => move(-1)} />
          <Title>Here is your budget</Title>

          <div className={cn('rounded-2xl p-5', over ? 'bg-warning-subtle' : 'bg-success-subtle')}>
            <p className="text-lg font-semibold text-ink-muted">{over ? 'Over each month' : 'Left over each month'}</p>
            <p
              className={cn(
                'numeric text-5xl font-bold tracking-[-0.03em]',
                over ? 'text-warning-dark' : 'text-success-dark',
              )}
            >
              {currency.format(Math.abs(sums.left))}
            </p>
            <p className="mt-2 text-lg text-ink">
              {over
                ? `You plan to spend ${currency.format(-sums.left)} more than comes in. Try lowering one or two items${biggestEveryday ? `, like ${biggestEveryday.name}` : ''}.`
                : 'Good. You plan to spend less than comes in.'}
            </p>
            {segments.length > 0 && (
              <>
                <div className="mt-4 flex h-4 gap-0.5 overflow-hidden rounded-full" aria-hidden>
                  {segments.map((s) => (
                    <span key={s.label} className={s.color} style={{ flexGrow: s.value }} />
                  ))}
                </div>
                <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-base text-ink-muted" aria-hidden>
                  {segments.map((s) => (
                    <li key={s.label} className="flex items-center gap-1.5">
                      <span className={cn('size-2.5 rounded-full', s.color)} />
                      {s.label}
                    </li>
                  ))}
                </ul>
              </>
            )}
          </div>

          <dl className="rounded-2xl border-[1.5px] border-border bg-surface px-5 py-2 text-lg">
            {rows.map(([label, value]) => (
              <div key={label} className="flex justify-between gap-4 border-b border-border py-3">
                <dt className="text-ink">{label}</dt>
                <dd className="numeric whitespace-nowrap font-semibold text-ink">{value}</dd>
              </div>
            ))}
            <div className="flex justify-between gap-4 py-3 text-xl font-bold">
              <dt>{over ? 'Over' : 'Left over'}</dt>
              <dd className="numeric whitespace-nowrap">{`${over ? '− ' : ''}${currency.format(Math.abs(sums.left))}`}</dd>
            </div>
          </dl>

          {save.error && (
            <Alert
              severity="error"
              title="Your budget was not saved"
              message={errorMessage(save.error, 'Please try again.')}
            />
          )}
        </div>
        <Footer key={screen}>
          {over ? (
            <>
              <Button size="lg" fullWidth className={BIG_BUTTON} onClick={() => setScreen('bills')}>
                Change something
              </Button>
              <Button
                variant="ghost"
                size="lg"
                fullWidth
                className={BIG_BUTTON}
                loading={save.isPending}
                onClick={() => save.mutate()}
              >
                Save anyway
              </Button>
            </>
          ) : (
            <>
              <Button
                size="lg"
                fullWidth
                className={BIG_BUTTON}
                loading={save.isPending}
                onClick={() => save.mutate()}
              >
                Save my budget
              </Button>
              <Button
                variant="secondary"
                size="lg"
                fullWidth
                className={BIG_BUTTON}
                onClick={() => setScreen('income')}
              >
                Change something
              </Button>
            </>
          )}
        </Footer>
      </>
    );
  }

  return (
    <div className="space-y-6 pt-8 text-center">
      <span className="mx-auto grid size-24 place-items-center rounded-full bg-success-subtle text-success-dark">
        <Check size={52} strokeWidth={3} aria-hidden />
      </span>
      <Title>Your budget is saved</Title>
      <p className="text-lg text-ink-muted">The money map now checks each month against it.</p>
      <Button component={Link} to="/" size="lg" fullWidth className={BIG_BUTTON}>
        See my money map
      </Button>
    </div>
  );
};

/**
 * A step-by-step budget builder that anyone can follow: one question per screen, big type, and amounts filled in
 * from the bank. It edits the same plan the Plan tab does.
 */
const BudgetBuilderPage = () => {
  const { data: plans, error: plansError } = useBudgetPlans();
  const { data: categories, error: categoriesError } = useCategories();
  const { data: map, error: mapError } = useMoneyMap(AVERAGE_MONTHS);
  const plan = useMemo(() => pickPlan(plans ?? []), [plans]);

  return (
    <div className="mx-auto w-full max-w-xl">
      {plans && categories && map ? (
        <Builder plan={plan} map={map} categories={categories} />
      ) : plansError || categoriesError || mapError ? (
        <Alert
          severity="error"
          title="Couldn't load your budget"
          message="Check that the server and database are running."
        />
      ) : (
        <div className="space-y-6" aria-busy>
          <Skeleton height={96} width={96} className="rounded-full" />
          <Skeleton height={36} />
          <Skeleton height={240} className="rounded-2xl" />
        </div>
      )}
    </div>
  );
};

export default BudgetBuilderPage;
