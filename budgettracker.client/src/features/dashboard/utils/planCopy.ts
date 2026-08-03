import { format, parseISO } from 'date-fns';

/**
 * Warm, encouraging headline based on how far spending has drifted from elapsed time.
 *
 * Keyed off `pacingDelta` rather than the API's `status` field on purpose: these seven
 * bands (±0.15, ±0.05, 0.3) are finer than the three status buckets (±0.05), so deriving
 * from `status` would silently collapse seven messages into three.
 */
export const getStatusHeadline = (
  pacingDelta: number,
  daysPct: number,
  planMonth: string,
): string => {
  const monthName = format(parseISO(planMonth), 'MMMM');

  if (daysPct === 0) return `${monthName} is just getting started`;
  if (pacingDelta <= -0.15) return `You're cruising in ${monthName}`;
  if (pacingDelta <= -0.05) return `Comfortably ahead in ${monthName}`;
  if (pacingDelta < 0.05) return `Right on the trail for ${monthName}`;
  if (pacingDelta < 0.15) return `A little brisk in ${monthName}`;
  if (pacingDelta < 0.3) return `Tightening up needed in ${monthName}`;
  return `Off the trail in ${monthName}`;
};
