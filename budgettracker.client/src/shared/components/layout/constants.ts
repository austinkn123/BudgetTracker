/**
 * Layout dimensions for the app shell (BUD-14).
 *
 * Exported as constants so the sidebar and the content area can never disagree
 * about the offset — the pre-BUD-14 layout inlined `280` in one place only.
 */

/** Sidebar width in px — permanent on desktop, drawer width on mobile. */
export const SIDEBAR_WIDTH = 240;

/** Height of the slim mobile top bar that hosts the hamburger button. */
export const MOBILE_TOPBAR_HEIGHT = 56;
