/**
 * The message to show a person when a request fails.
 *
 * Prefers the server's deliberate, human-readable `error` field; otherwise the fallback.
 * Never surfaces `Error.message`: that is exception text, not copy (design.md §2).
 */
export const errorMessage = (err: unknown, fallback: string): string => {
  const serverError = (err as { response?: { data?: { error?: unknown } } })?.response?.data?.error;
  return typeof serverError === 'string' && serverError.trim() !== '' ? serverError : fallback;
};
