import type { ReactNode } from 'react';
import { Wallet } from 'lucide-react';

type AuthLayoutProps = {
  title: string;
  description: ReactNode;
  children: ReactNode;
};

const Brand = ({ className, iconClassName }: { className?: string; iconClassName: string }) => (
  <div className={`flex items-center gap-2.5 ${className ?? ''}`}>
    <Wallet size={22} className={iconClassName} />
    <span className="text-[17px] font-semibold tracking-[-0.01em]">BudgetTracker</span>
  </div>
);

/**
 * The idea the app is built on, drawn once: how far through the month you are against how much
 * of the plan is spent. Static example figures, labelled as such; the bars draw in on load.
 */
const PaceExample = () => (
  <figure className="rounded-xl bg-white/[0.04] p-6 ring-1 ring-white/10">
    <figcaption className="text-2xs font-semibold uppercase tracking-[0.08em] text-white/50">
      Example month
    </figcaption>
    <dl className="mt-5 space-y-5">
      <div>
        <div className="flex items-baseline justify-between text-sm">
          <dt className="text-white/70">Month gone</dt>
          <dd className="numeric font-semibold text-white">18 of 30 days</dd>
        </div>
        <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-white/10">
          <div className="h-full w-[60%] origin-left animate-grow-x rounded-full bg-white/40" />
        </div>
      </div>
      <div>
        <div className="flex items-baseline justify-between text-sm">
          <dt className="text-white/70">Plan spent</dt>
          <dd className="numeric font-semibold text-white">$2,431 of $4,678</dd>
        </div>
        <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-white/10">
          <div className="h-full w-[52%] origin-left animate-grow-x rounded-full bg-primary-light [animation-delay:120ms]" />
        </div>
      </div>
    </dl>
    <p className="mt-5 text-sm text-white/70">
      Spending slower than the month. About $625 of room at this pace.
    </p>
  </figure>
);

/**
 * Shell for the signed-out pages. On large screens the left panel says what the app does; on
 * small screens it drops away and the form is the whole page.
 */
const AuthLayout = ({ title, description, children }: AuthLayoutProps) => (
  <div className="grid min-h-[100dvh] bg-background lg:grid-cols-[minmax(0,5fr)_minmax(0,6fr)]">
    <aside className="hidden flex-col justify-between gap-12 bg-grey-900 p-12 text-white lg:flex">
      <Brand iconClassName="text-primary-light" />
      <div className="max-w-md animate-rise-in">
        <h2 className="text-[28px] font-semibold leading-tight tracking-[-0.025em]">
          Keep the books on the plan you already made.
        </h2>
        <p className="mt-3 text-body text-white/70">
          Every transaction lands against your plan, so a drifting category shows up before it costs
          you the month.
        </p>
      </div>
      <PaceExample />
    </aside>

    <main className="flex items-center justify-center px-4 py-10 sm:px-8">
      <div className="w-full max-w-[400px] animate-rise-in">
        <Brand className="mb-10 text-ink lg:hidden" iconClassName="text-primary" />
        <h1 className="text-[22px] font-semibold tracking-[-0.02em] text-ink">{title}</h1>
        <p className="mt-1.5 text-sm text-ink-muted">{description}</p>
        <div className="mt-8 flex flex-col gap-6">{children}</div>
      </div>
    </main>
  </div>
);

export default AuthLayout;
