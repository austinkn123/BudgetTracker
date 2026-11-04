import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import Tabs from './Tabs';

const OPTIONS = [
  { value: 'alpha', label: 'Alpha' },
  { value: 'beta', label: 'Beta' },
  { value: 'gamma', label: 'Gamma' },
] as const;

type Section = (typeof OPTIONS)[number]['value'];

/** Owns the selection, as a page does, and renders only the selected section's content. */
const Harness = () => {
  const [value, setValue] = useState<Section>('alpha');
  return (
    <Tabs value={value} onChange={setValue} options={OPTIONS} ariaLabel="Sections">
      <p>{`${value} content`}</p>
    </Tabs>
  );
};

const tab = (name: string) => screen.getByRole('tab', { name });
const tabs = () => screen.getAllByRole('tab');

/** Renders the harness with the selected (first) tab focused, as a keyboard user lands on it. */
const renderFocused = async () => {
  const user = userEvent.setup();
  render(<Harness />);
  await user.tab();
  expect(tab('Alpha')).toHaveFocus();
  return user;
};

describe('Tabs', () => {
  it('wraps from the first tab to the last on ArrowLeft, selecting and focusing it', async () => {
    const user = await renderFocused();

    await user.keyboard('{ArrowLeft}');

    expect(tab('Gamma')).toHaveFocus();
    expect(tab('Gamma')).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('gamma content')).toBeInTheDocument();
  });

  it('jumps to the last tab on End and the first on Home, selecting and focusing each', async () => {
    const user = await renderFocused();

    await user.keyboard('{End}');
    expect(tab('Gamma')).toHaveFocus();
    expect(tab('Gamma')).toHaveAttribute('aria-selected', 'true');

    await user.keyboard('{Home}');
    expect(tab('Alpha')).toHaveFocus();
    expect(tab('Alpha')).toHaveAttribute('aria-selected', 'true');
  });

  it('keeps only the selected tab in the tab order', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    expect(tabs().map((t) => t.tabIndex)).toEqual([0, -1, -1]);

    await user.click(tab('Beta'));

    expect(tabs().map((t) => t.tabIndex)).toEqual([-1, 0, -1]);
  });

  it('pairs the selected tab and the panel through aria-controls and aria-labelledby', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    for (const name of ['Alpha', 'Gamma']) {
      await user.click(tab(name));
      const panel = screen.getByRole('tabpanel', { name });
      expect(tab(name)).toHaveAttribute('aria-controls', panel.id);
      expect(panel).toHaveAttribute('aria-labelledby', tab(name).id);
      // No dangling reference: an unselected tab has no panel in the DOM to point at.
      expect(
        tabs().filter((t) => t !== tab(name) && t.hasAttribute('aria-controls')),
      ).toHaveLength(0);
    }
  });

  it('renders only the selected panel', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    // hidden: true, so a panel kept in the DOM but hidden still counts.
    expect(screen.getAllByRole('tabpanel', { hidden: true })).toHaveLength(1);

    await user.click(tab('Beta'));

    expect(screen.getAllByRole('tabpanel', { hidden: true })).toHaveLength(1);
    expect(screen.getByRole('tabpanel')).toHaveTextContent('beta content');
    expect(screen.queryByText('alpha content')).not.toBeInTheDocument();
  });
});
