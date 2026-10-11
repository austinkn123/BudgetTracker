/**
 * The message to show a person when a request fails.
 *
 * Prefers the server's deliberate, human-readable `error` field, then a ProblemDetails `detail`
 * (the Plaid link-token routes' 502); otherwise the fallback.
 * Never surfaces `Error.message`: that is exception text, not copy (design.md §2).
 */
const isMessage = (value: unknown): value is string =>
  typeof value === 'string' && value.trim() !== '';

export const errorMessage = (err: unknown, fallback: string): string => {
  const data = (err as { response?: { data?: { error?: unknown; detail?: unknown } } })?.response
    ?.data;
  return [data?.error, data?.detail].find(isMessage) ?? fallback;
};
