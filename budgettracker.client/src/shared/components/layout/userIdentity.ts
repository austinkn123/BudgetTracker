/**
 * Display helpers for the nav account cluster (BUD-14).
 *
 * The app has no display name and no avatar URL — Cognito collects
 * given_name/family_name at sign-up but never reads them back — so the footer
 * is built from the email address alone.
 */

const FALLBACK_LABEL = 'Account';

/**
 * Initials for the avatar, derived from the local part of an email.
 * `ada.lovelace@x.com` -> `AL`, `ada@x.com` -> `AD`.
 */
export const getInitials = (email: string | null | undefined): string => {
  const localPart = email?.split('@')[0]?.trim();
  if (!localPart) return '';

  const tokens = localPart.split(/[._+-]+/).filter(Boolean);
  if (tokens.length >= 2) {
    return `${tokens[0][0]}${tokens[1][0]}`.toUpperCase();
  }

  return localPart.slice(0, 2).toUpperCase();
};

/**
 * Label shown beneath the avatar. Prefers the backend email, then the Cognito
 * one (which is empty after a refresh — AuthContext cannot repopulate
 * signInDetails from getCurrentUser), then a generic fallback.
 */
export const getAccountLabel = (
  email: string | null | undefined,
  fallback?: string | null,
): string => email?.trim() || fallback?.trim() || FALLBACK_LABEL;
