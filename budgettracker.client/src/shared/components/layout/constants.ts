/**
 * Layout dimensions for the app shell (BUD-14, BUD-20).
 *
 * Exported as constants so the floating nav and the content area can never
 * disagree about the offset. Numbers (not classes) because the nav animates
 * between the two sizes.
 */

/** Nav height at rest. */
export const NAV_HEIGHT_FULL = 56;

/** Nav height once contracted by scrolling. */
export const NAV_HEIGHT_COMPACT = 46;

/** Nav width at rest — matches the content container (max-w-7xl). */
export const NAV_MAX_WIDTH_FULL = 1280;

/** Contracted width: wide enough for icons + account cluster, nothing more. */
export const NAV_MAX_WIDTH_COMPACT = 460;

/** Gap between the viewport top and the floating bar. */
export const NAV_TOP_OFFSET = 12;

/** Width of the mobile navigation sheet. */
export const MOBILE_NAV_WIDTH = 268;
