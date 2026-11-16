import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Button, Card } from './ui';

export interface ErrorBoundaryProps {
  children: ReactNode;
  /**
   * When this changes, a caught error is cleared and the children render again. Pass the route
   * path so navigating away from a broken page recovers without a reload.
   */
  resetKey?: string;
}

interface ErrorBoundaryState {
  error: Error | null;
  resetKey: string | undefined;
}

/**
 * Catches render errors below it and shows a fallback instead of a blank screen
 * (project-docs/observability.md §6). The error stays in the browser console only: there is
 * deliberately no client-error endpoint while the app is single-user; Sentry replaces this before
 * a multi-user launch.
 *
 * A class component on purpose: React only supports error boundaries through
 * `getDerivedStateFromError` / `componentDidCatch`, which have no hook equivalent.
 */
class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null, resetKey: this.props.resetKey };

  static getDerivedStateFromError(error: Error): Partial<ErrorBoundaryState> {
    return { error };
  }

  static getDerivedStateFromProps(
    props: ErrorBoundaryProps,
    state: ErrorBoundaryState,
  ): Partial<ErrorBoundaryState> | null {
    // A new reset key (e.g. a new route) clears the error so the new page gets a fresh render.
    if (props.resetKey !== state.resetKey) {
      return { error: null, resetKey: props.resetKey };
    }
    return null;
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Render error caught by ErrorBoundary', error, info.componentStack);
  }

  private readonly reset = () => this.setState({ error: null });

  render() {
    if (!this.state.error) return this.props.children;

    return (
      <div role="alert" className="mx-auto max-w-md py-16">
        <Card>
          <div className="flex flex-col gap-3">
            <h2 className="text-lg font-semibold text-ink">Something went wrong</h2>
            <p className="text-body text-ink-muted">
              This part of the page failed to load. Try again, or reload the page if it keeps
              happening.
            </p>
            <div className="flex gap-2 pt-1">
              <Button onClick={this.reset}>Try again</Button>
              <Button variant="secondary" onClick={() => window.location.reload()}>
                Reload page
              </Button>
            </div>
          </div>
        </Card>
      </div>
    );
  }
}

export default ErrorBoundary;
