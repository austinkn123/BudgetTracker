import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

/** A QueryClient with retries off so failing requests settle immediately in tests. */
export const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: Infinity },
      mutations: { retry: false },
    },
  });

/** Wrapper factory for renderHook/render that provides the given QueryClient. */
export const createQueryWrapper = (queryClient: QueryClient) => {
  const QueryWrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  return QueryWrapper;
};
