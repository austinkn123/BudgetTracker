import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import ErrorBoundary from './ErrorBoundary';

let shouldThrow = true;

const Boom = () => {
  if (shouldThrow) throw new Error('render failed');
  return <p>Recovered content</p>;
};

describe('ErrorBoundary', () => {
  let consoleError: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    shouldThrow = true;
    // React logs caught render errors; silence it so the test output stays readable.
    consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    consoleError.mockRestore();
  });

  it('renders its children when nothing throws', () => {
    shouldThrow = false;
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByText('Recovered content')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows a fallback instead of a blank screen when a child throws', () => {
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByRole('alert')).toHaveTextContent('Something went wrong');
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reload page' })).toBeInTheDocument();
  });

  it('keeps the error in the browser console and nowhere else', () => {
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    const logged = consoleError.mock.calls.flat();
    expect(logged.some((arg: unknown) => arg instanceof Error && arg.message === 'render failed')).toBe(true);
  });

  it('renders the children again after "Try again" once the cause is gone', async () => {
    const user = userEvent.setup();
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    shouldThrow = false;
    await user.click(screen.getByRole('button', { name: 'Try again' }));

    expect(screen.getByText('Recovered content')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('clears the error when its reset key changes, e.g. on navigation', () => {
    const { rerender } = render(
      <ErrorBoundary resetKey="/transactions">
        <Boom />
      </ErrorBoundary>,
    );
    expect(screen.getByRole('alert')).toBeInTheDocument();

    shouldThrow = false;
    rerender(
      <ErrorBoundary resetKey="/settings">
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByText('Recovered content')).toBeInTheDocument();
  });

  it('stays on the fallback when re-rendered with the same reset key', () => {
    const { rerender } = render(
      <ErrorBoundary resetKey="/transactions">
        <Boom />
      </ErrorBoundary>,
    );

    shouldThrow = false;
    rerender(
      <ErrorBoundary resetKey="/transactions">
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});
