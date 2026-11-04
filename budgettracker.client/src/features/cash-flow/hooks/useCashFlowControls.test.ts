import { act, renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { useCashFlowControls } from './useCashFlowControls';

describe('useCashFlowControls grouping', () => {
  beforeEach(() => window.localStorage.clear());

  it('drops a manual grouping when the range changes', () => {
    const { result } = renderHook(() => useCashFlowControls());
    act(() => result.current.setGrouping('Year'));
    expect(result.current.query.groupBy).toBe('Year');

    act(() => result.current.setRange('3m'));

    expect(result.current.grouping).toBe('Week');
    expect(result.current.query.groupBy).toBe('Week');
  });

  it('drops a manual grouping when custom dates are picked', () => {
    const { result } = renderHook(() => useCashFlowControls());
    act(() => result.current.setGrouping('Year'));

    act(() =>
      result.current.setCustomRange({ from: new Date(2026, 0, 1), to: new Date(2026, 0, 20) }),
    );

    expect(result.current.query.groupBy).toBe('Day');
  });

  it('keeps a manual grouping when only a filter changes', () => {
    const { result } = renderHook(() => useCashFlowControls());
    act(() => result.current.setGrouping('Week'));

    act(() => result.current.setAccountId('chk'));
    act(() => result.current.setCategoryId(3));

    expect(result.current.query.groupBy).toBe('Week');
  });
});
