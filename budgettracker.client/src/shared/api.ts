import axios, { AxiosHeaders, isAxiosError } from 'axios';
import { fetchAuthSession, signOut } from 'aws-amplify/auth';

/**
 * Header that ties a browser request to its server log lines (project-docs/observability.md,
 * Phase 1). The server reads it, echoes it on the response, and includes it in ProblemDetails.
 */
export const CORRELATION_ID_HEADER = 'X-Correlation-ID';

const api = axios.create({
  baseURL: '/api',
});

api.interceptors.request.use(async (config) => {
  if (!config.headers.has(CORRELATION_ID_HEADER)) {
    config.headers.set(CORRELATION_ID_HEADER, crypto.randomUUID());
  }

  try {
    const session = await fetchAuthSession();
    const idToken = session.tokens?.idToken?.toString();
    if (idToken) {
      config.headers.Authorization = `Bearer ${idToken}`;
    }
  } catch (error) {
    console.error('Failed to get auth token:', error);
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    logFailedRequest(error);
    if (error.response?.status === 401) {
      await signOut();
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

/**
 * Logs just enough to find the matching CloudWatch line: method, path (no query string),
 * status and correlation id. Never the request/response body, query params or headers,
 * which can carry amounts, payees, account names, emails or tokens.
 */
function logFailedRequest(error: unknown): void {
  if (!isAxiosError(error)) return;

  const echoed = error.response?.headers
    ? AxiosHeaders.from(error.response.headers as AxiosHeaders).get(CORRELATION_ID_HEADER)
    : undefined;
  const sent = error.config?.headers?.get(CORRELATION_ID_HEADER);
  const correlationId = String(echoed ?? sent ?? 'unknown');

  const method = error.config?.method?.toUpperCase() ?? 'REQUEST';
  const path = (error.config?.url ?? '').split('?')[0];
  const status = error.response?.status ?? error.code ?? 'network error';

  console.error(`API ${method} ${path} failed (${status}) correlationId=${correlationId}`);
}

export default api;
