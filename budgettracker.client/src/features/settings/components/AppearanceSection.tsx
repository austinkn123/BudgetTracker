import { Card, ToggleGroup } from '../../../shared/components/ui';
import { useTheme } from '../../../shared/theme/useTheme';
import type { ThemePreference } from '../../../shared/theme/theme';

const OPTIONS: readonly { value: ThemePreference; label: string }[] = [
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
  { value: 'system', label: 'System' },
];

/**
 * Theme preference control (BUD-17).
 *
 * `system` is the default and keeps tracking the OS setting for the rest of the
 * session; picking light or dark pins it and persists to localStorage.
 */
const AppearanceSection = () => {
  const { preference, theme, setPreference } = useTheme();

  return (
    <Card title="Appearance" subtitle="Choose how BudgetTracker looks on this device">
      <div className="flex flex-wrap items-center gap-4">
        <ToggleGroup
          value={preference}
          onChange={setPreference}
          options={OPTIONS}
          ariaLabel="Theme preference"
        />
        {preference === 'system' && (
          <span className="text-[13px] text-ink-muted">
            Following your system setting — currently {theme}.
          </span>
        )}
      </div>
    </Card>
  );
};

export default AppearanceSection;
