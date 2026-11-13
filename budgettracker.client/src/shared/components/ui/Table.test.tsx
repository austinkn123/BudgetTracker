import type { ComponentProps } from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import Table from './Table';
import type { TableColumn } from './Table';

interface Row {
  id: number;
  merchant: string | null;
  amount: number;
}

const ROWS: readonly Row[] = [
  { id: 1, merchant: 'Corner Market', amount: 42.5 },
  { id: 2, merchant: 'Landlord', amount: 1800 },
];

const COLUMNS: readonly TableColumn<Row>[] = [
  { key: 'merchant', header: 'Merchant' },
  { key: 'amount', header: 'Amount', align: 'right', render: (row) => row.amount.toFixed(2) },
  {
    key: 'actions',
    header: 'Actions',
    render: (row) => <button type="button">{`Edit ${row.id}`}</button>,
  },
];

const renderTable = (props: Partial<ComponentProps<typeof Table<Row>>> = {}) =>
  render(
    <Table<Row>
      columns={COLUMNS}
      rows={ROWS}
      rowKey={(row) => row.id}
      ariaLabel="Transactions"
      {...props}
    />,
  );

/** The tbody; the thead is the first rowgroup. */
const body = () => screen.getAllByRole('rowgroup')[1];
const bodyRows = () => within(body()).getAllByRole('row');

describe('Table', () => {
  it('renders a labelled table with column headers and a row per item', () => {
    renderTable();

    const table = screen.getByRole('table', { name: 'Transactions' });
    expect(within(table).getAllByRole('columnheader').map((th) => th.textContent)).toEqual([
      'Merchant',
      'Amount',
      'Actions',
    ]);
    expect(bodyRows()).toHaveLength(2);
    // A key-only column prints the raw field; a render column uses its renderer.
    expect(within(bodyRows()[0]).getAllByRole('cell').map((td) => td.textContent)).toEqual([
      'Corner Market',
      '42.50',
      'Edit 1',
    ]);
    expect(within(bodyRows()[1]).getAllByRole('cell')[1]).toHaveClass('text-right');
  });

  it('shows the empty message in one full-width cell instead of an empty body', () => {
    renderTable({ rows: [], emptyMessage: 'No transactions yet' });

    expect(bodyRows()).toHaveLength(1);
    const cells = within(bodyRows()[0]).getAllByRole('cell');
    expect(cells).toHaveLength(1);
    expect(cells[0]).toHaveAttribute('colspan', String(COLUMNS.length));
    expect(cells[0]).toHaveTextContent('No transactions yet');
  });

  it('falls back to "No data" when no empty message is given', () => {
    renderTable({ rows: [] });

    expect(within(body()).getByText('No data')).toBeInTheDocument();
  });

  it('renders skeleton rows while loading, hiding both rows and the empty message', () => {
    renderTable({ loading: true, emptyMessage: 'No transactions yet' });

    expect(bodyRows()).toHaveLength(3);
    expect(within(bodyRows()[0]).getAllByRole('cell')).toHaveLength(COLUMNS.length);
    expect(screen.queryByText('Corner Market')).not.toBeInTheDocument();
    expect(screen.queryByText('No transactions yet')).not.toBeInTheDocument();
  });

  it('calls onRowClick with the clicked row', async () => {
    const user = userEvent.setup();
    const onRowClick = vi.fn();
    renderTable({ onRowClick });

    await user.click(screen.getByText('Landlord'));

    expect(onRowClick).toHaveBeenCalledExactlyOnceWith(ROWS[1]);
  });

  it('prints an empty cell for a null field rather than "null"', () => {
    renderTable({ rows: [{ id: 3, merchant: null, amount: 0 }] });

    expect(within(bodyRows()[0]).getAllByRole('cell')[0]).toBeEmptyDOMElement();
  });
});
