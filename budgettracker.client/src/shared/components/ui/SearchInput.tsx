import { Search, X } from 'lucide-react';
import { cn } from '../../utils/cn';

export interface SearchInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  ariaLabel: string;
  className?: string;
}

/** Standalone search box with a clear button, for toolbars outside a form. */
const SearchInput = ({ value, onChange, placeholder, ariaLabel, className }: SearchInputProps) => (
  <div className={cn('relative', className)}>
    <Search
      size={16}
      className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-muted"
    />
    <input
      type="search"
      value={value}
      onChange={(event) => onChange(event.target.value)}
      placeholder={placeholder}
      aria-label={ariaLabel}
      className="focus-ring h-10 w-full rounded-lg border border-border bg-surface pl-9 pr-9 text-[14px] text-ink shadow-xs transition-colors duration-120 hover:border-border-strong placeholder:text-ink-muted/60"
    />
    {value && (
      <button
        type="button"
        onClick={() => onChange('')}
        aria-label="Clear search"
        className="focus-ring absolute right-2 top-1/2 -translate-y-1/2 rounded p-1 text-ink-muted hover:text-ink"
      >
        <X size={14} />
      </button>
    )}
  </div>
);

export default SearchInput;
