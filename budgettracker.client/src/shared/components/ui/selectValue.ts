/**
 * Radix Select.Item throws on value="" (reserved for clearing), so the empty
 * option uses a sentinel translated back at the form boundary.
 */
export const EMPTY_SENTINEL = '__empty__';

/**
 * Maps a select value to Radix's string value. An unset value (null/undefined/'') selects
 * the empty option when there is one; otherwise it becomes '' so Radix renders the placeholder.
 */
export const toRadixValue = (value: unknown, hasEmptyOption: boolean): string => {
  if (value == null || value === '') return hasEmptyOption ? EMPTY_SENTINEL : '';
  return String(value);
};
