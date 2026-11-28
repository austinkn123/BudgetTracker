/** The Cognito user pool group whose members can read feedback. Mirrors FeedbackEndpoints.AdminGroup. */
export const ADMIN_GROUP = 'admin';

/**
 * Whether the ID token's `cognito:groups` includes the admin group. Only decides what the UI shows:
 * the server enforces the same check on every admin request, so a forged token gains nothing.
 */
export const isAdminToken = (idToken: string | null | undefined): boolean => {
  const payload = idToken?.split('.')[1];
  if (!payload) return false;
  try {
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(payload.length / 4) * 4, '='));
    const groups: unknown = (JSON.parse(json) as Record<string, unknown>)['cognito:groups'];
    return Array.isArray(groups) && groups.includes(ADMIN_GROUP);
  } catch {
    return false;
  }
};
