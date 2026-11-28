import { Alert, Button } from './ui';

type LoadErrorProps = {
  /** What failed to load, read after "Couldn't load", e.g. "your budget". */
  what: string;
  onRetry: () => void;
  className?: string;
};

/** A failed read: says what is missing in plain words and offers a retry. Never shown alongside an empty state. */
const LoadError = ({ what, onRetry, className }: LoadErrorProps) => (
  <Alert severity="error" title={`Couldn't load ${what}`} className={className}>
    <p>This is usually temporary. Try again in a moment.</p>
    <Button variant="secondary" size="sm" onClick={onRetry} className="mt-3">
      Try again
    </Button>
  </Alert>
);

export default LoadError;
