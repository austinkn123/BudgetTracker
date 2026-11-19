import type { InternalAxiosRequestConfig } from 'axios';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// Capture the fully resolved request URL (base + path + query string) without touching the network.
const captured = vi.hoisted(() => ({ urls: [] as string[] }));

vi.mock('../api', async () => {
  const { default: axios } = await import('axios');
  const instance = axios.create({ baseURL: '/api' });
  instance.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
    captured.urls.push(instance.getUri(config));
    return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
  };
  return { default: instance };
});

import { budgetAnalysisService } from './budgetAnalysis.service';
import { cashFlowService } from './cashFlow.service';
import { moneyMapService } from './moneyMap.service';

// The server's clock is UTC, which is already tomorrow every US evening; these reads send the user's own date.
describe("reads that depend on 'today' send the browser's date", () => {
  beforeEach(() => {
    captured.urls.length = 0;
    vi.useFakeTimers({ now: new Date(2026, 10, 15, 21, 0) });
  });
  afterEach(() => vi.useRealTimers());

  it('budget analysis', async () => {
    await budgetAnalysisService.getAnalysis('2026-11-01', '2026-11-30', 3);

    expect(captured.urls).toEqual(['/api/budget-analysis?from=2026-11-01&to=2026-11-30&trendMonths=3&today=2026-11-15']);
  });

  it('money map', async () => {
    await moneyMapService.get(6);

    expect(captured.urls).toEqual(['/api/money-map?months=6&today=2026-11-15']);
  });

  it('cash flow', async () => {
    await cashFlowService.getReport({ to: '2026-11-30', groupBy: 'Week' });

    expect(captured.urls).toEqual(['/api/cash-flow?to=2026-11-30&groupBy=Week&today=2026-11-15']);
  });
});
