import { TooltipProvider } from '@radix-ui/react-tooltip';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, describe, expect, it } from 'vitest';

import Tooltip from './Tooltip';

// Radix Tooltip positions with floating-ui, which observes size; jsdom has no ResizeObserver.
beforeAll(() => {
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});

const TEXT = 'Pending transactions are excluded';

// main.tsx mounts the provider app-wide; a zero delay keeps the tests fast.
const renderTooltip = () =>
  render(
    <TooltipProvider delayDuration={0}>
      <Tooltip title={TEXT}>
        <button type="button">Info</button>
      </Tooltip>
    </TooltipProvider>,
  );

const trigger = () => screen.getByRole('button', { name: 'Info' });

describe('Tooltip', () => {
  it('renders only the trigger until hovered or focused', () => {
    renderTooltip();

    expect(trigger()).toBeInTheDocument();
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });

  it('shows on keyboard focus and describes the trigger', async () => {
    const user = userEvent.setup();
    renderTooltip();

    await user.tab();

    expect(await screen.findByRole('tooltip')).toHaveTextContent(TEXT);
    expect(trigger()).toHaveAccessibleDescription(TEXT);
  });

  it('shows on hover', async () => {
    const user = userEvent.setup();
    renderTooltip();

    await user.hover(trigger());

    expect(await screen.findByRole('tooltip')).toHaveTextContent(TEXT);
  });

  it('dismisses on Escape', async () => {
    const user = userEvent.setup();
    renderTooltip();
    await user.tab();
    await screen.findByRole('tooltip');

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });
});
