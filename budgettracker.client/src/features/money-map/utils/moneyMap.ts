import { addMonths, eachMonthOfInterval, isSameMonth, parseISO } from 'date-fns';
import type { MoneyMap, MoneyMapLine, UpcomingItem } from '../../../shared/types/api';
import { currency } from '../../../shared/utils/format';

/**
 * Presentation over the server's money map: grouping, ordering and wording. The server computes every
 * average; sums here only add up lines already computed.
 */

export interface Split {
  mustPay: number;
  flexible: number;
  saving: number;
}

/** A line runs over when its average beats the plan by more than this share. */
const OVER_TOLERANCE = 0.05;

const sum = (lines: MoneyMapLine[], pick: (l: MoneyMapLine) => number) =>
  lines.reduce((total, line) => total + pick(line), 0);

/** A normal month's pay: the plan's figure, or what actually landed when there is no plan. */
export const normalIncome = (map: MoneyMap) => map.plannedIncomeMonthly || map.actualIncomeMonthly;

/** The plan and the actual averages split three ways; unplanned spend counts as flexible. */
export const splitMonth = (map: MoneyMap): { plan: Split; actual: Split } => {
  const income = normalIncome(map);
  const core = map.lines.filter((l) => l.bucket === 'Core');
  const flexible = map.lines.filter((l) => l.bucket !== 'Core');
  const split = (pick: (l: MoneyMapLine) => number): Split => {
    const mustPay = sum(core, pick);
    const flex = sum(flexible, pick);
    return { mustPay, flexible: flex, saving: income - mustPay - flex };
  };
  return { plan: split((l) => l.planMonthly), actual: split((l) => l.actualMonthly) };
};

export type Tone = 'success' | 'warning' | 'info';

export const lineStatus = (line: MoneyMapLine): { tone: Tone; label: string } => {
  if (line.planMonthly === 0) return { tone: 'warning', label: 'Not in the plan' };
  if (line.actualMonthly <= 0)
    return { tone: 'info', label: line.cadence === 'Annual' ? 'Set aside monthly' : 'Nothing spent' };
  const over = line.actualMonthly - line.planMonthly;
  if (over > line.planMonthly * OVER_TOLERANCE) return { tone: 'warning', label: `${currency.format(over)} over` };
  if (over < -line.planMonthly * OVER_TOLERANCE)
    return { tone: 'success', label: `${currency.format(-over)} under` };
  return { tone: 'success', label: 'On track' };
};

const isOver = (line: MoneyMapLine) => lineStatus(line).tone === 'warning';

export interface LineGroup {
  key: 'Core' | 'Buffer' | 'Unplanned';
  label: string;
  lines: MoneyMapLine[];
}

/** Must-pay, flexible, then unplanned. Lines that need attention lead their group; the rest keep plan order. */
export const groupLines = (lines: MoneyMapLine[]): LineGroup[] => {
  const attentionFirst = (group: MoneyMapLine[]) =>
    [...group].sort((a, b) => Number(isOver(b)) - Number(isOver(a)));
  const groups: LineGroup[] = [
    { key: 'Core', label: 'Must-pay', lines: attentionFirst(lines.filter((l) => l.bucket === 'Core')) },
    { key: 'Buffer', label: 'Flexible', lines: attentionFirst(lines.filter((l) => l.bucket === 'Buffer')) },
    { key: 'Unplanned', label: 'Not in the plan', lines: lines.filter((l) => l.bucket === null) },
  ];
  return groups.filter((g) => g.lines.length > 0);
};

/** The planned line that is furthest over its plan, if any. */
export const biggestOverrun = (lines: MoneyMapLine[]) =>
  lines
    .filter((l) => l.planMonthly > 0 && isOver(l))
    .sort((a, b) => b.actualMonthly - b.planMonthly - (a.actualMonthly - a.planMonthly))[0] ?? null;

/** What to set aside each month so every lump-sum (Annual) bill is covered when it lands. */
export const billsSetAside = (lines: MoneyMapLine[]) =>
  sum(lines.filter((l) => l.cadence === 'Annual'), (l) => l.planMonthly);

export interface TimelineMonth {
  month: Date;
  items: UpcomingItem[];
}

/**
 * The months the upcoming items span: this month through 12 months out, dropping this month when nothing
 * is still due in it so the strip stays a year wide.
 */
export const timeline = (upcoming: UpcomingItem[], today: Date): TimelineMonth[] => {
  const months = eachMonthOfInterval({ start: today, end: addMonths(today, 12) }).map((month) => ({
    month,
    items: upcoming.filter((u) => isSameMonth(parseISO(u.date), month)),
  }));
  return months[0].items.length === 0 ? months.slice(1) : months;
};
