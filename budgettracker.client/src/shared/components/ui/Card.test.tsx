import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import Button from './Button';
import Card from './Card';

describe('Card', () => {
  it('renders title as a heading, subtitle, actions, body and footer', () => {
    render(
      <Card title="Spending" subtitle="This month" actions={<Button>Edit</Button>} footer="Updated today">
        <p>Body</p>
      </Card>,
    );

    expect(screen.getByRole('heading', { level: 2, name: 'Spending' })).toBeInTheDocument();
    expect(screen.getByText('This month')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
    expect(screen.getByText('Body')).toBeInTheDocument();
    expect(screen.getByText('Updated today')).toBeInTheDocument();
  });

  it('renders no header and no button when given only children', () => {
    render(<Card>Just content</Card>);

    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByText('Just content')).toBeInTheDocument();
  });

  it('becomes a single button when onClick is given', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(
      <Card title="Net worth" onClick={onClick}>
        $1,000
      </Card>,
    );

    const button = screen.getByRole('button', { name: /Net worth/ });
    expect(button).toHaveAttribute('type', 'button');
    await user.click(button);

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('maps the padding token to spacing utilities', () => {
    const { rerender } = render(<Card padding="lg">Body</Card>);
    expect(screen.getByText('Body')).toHaveClass('px-8', 'py-7');

    rerender(<Card padding="none">Body</Card>);
    expect(screen.getByText('Body')).not.toHaveClass('px-8');
  });
});
