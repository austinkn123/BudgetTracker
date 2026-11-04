import { useSearchParams } from 'react-router-dom';

/**
 * A page tab kept in `?tab=` so a reload keeps it. The first tab is the bare URL, and anything
 * unrecognised falls back to it. Switching replaces history rather than stacking an entry per tab.
 */
export const useTabParam = <TTab extends string>(tabs: readonly [TTab, ...TTab[]]) => {
  const [params, setParams] = useSearchParams();
  const tab = tabs.find((t) => t === params.get('tab')) ?? tabs[0];
  const selectTab = (next: TTab) =>
    setParams(
      (current) => {
        if (next === tabs[0]) current.delete('tab');
        else current.set('tab', next);
        return current;
      },
      { replace: true },
    );
  return [tab, selectTab] as const;
};
