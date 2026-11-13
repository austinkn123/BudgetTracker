import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import PageHeader from './PageHeader';

describe('PageHeader', () => {
  it('renders the title as the page heading with its description and actions', () => {
    render(
      <PageHeader
        title="Cash flow"
        description="Money in and out this month"
        actions={<button type="button">Export</button>}
      />,
    );

    expect(screen.getByRole('heading', { level: 1, name: 'Cash flow' })).toBeInTheDocument();
    expect(screen.getByText('Money in and out this month')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Export' })).toBeInTheDocument();
  });

  it('renders only the heading when description and actions are omitted', () => {
    const { container } = render(<PageHeader title="Budget" />);

    expect(screen.getByRole('heading', { level: 1, name: 'Budget' })).toBeInTheDocument();
    expect(container.querySelector('p')).toBeNull();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
