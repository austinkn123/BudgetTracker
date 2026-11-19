import { format } from 'date-fns';

/**
 * The browser's calendar date (yyyy-MM-dd), sent as `today` to reads that depend on it. The server's own clock is UTC,
 * which is already tomorrow every US evening; it accepts this when it is within a day of UTC.
 */
export const localToday = (): string => format(new Date(), 'yyyy-MM-dd');
