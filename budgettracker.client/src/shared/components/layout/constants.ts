/**
 * Layout dimensions for the app shell (BUD-14, BUD-20).
 *
 * Exported as constants so the sidebar and the content area can never disagree
 * about the offset.
 */

/** Expanded sidebar width in px — also the mobile sheet width. */
export const SIDEBAR_WIDTH = 240;

/** Collapsed icon-rail width in px (BUD-20 scroll-aware nav). */
export const SIDEBAR_RAIL_WIDTH = 68;

/** Height of the slim mobile top bar that hosts the hamburger button. */
export const MOBILE_TOPBAR_HEIGHT = 56;

/** localStorage key for the user's explicit expand/collapse choice. */
export const SIDEBAR_PIN_KEY = 'bud_sidebar_pinned';
