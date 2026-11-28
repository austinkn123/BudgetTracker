import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import LoadError from './LoadError';

describe('LoadError', () => {
  it('names what failed and retries on click', async () => {
    const onRetry = vi.fn();
    render(<LoadError what="your budget" onRetry={onRetry} />);

    expect(screen.getByRole('alert')).toHaveTextContent("Couldn't load your budget");
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});
