import type { ReactNode } from 'react';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Skeleton from '@mui/material/Skeleton';
import Typography from '@mui/material/Typography';
import type { SxProps, Theme } from '@mui/material/styles';

interface BudTableColumnBase {
  header: ReactNode;
  width?: string | number;
  align?: 'left' | 'center' | 'right';
}

/**
 * Either `key` names a real field of TRow (so `render` may be omitted and the
 * raw value is printed), or it is a synthetic id and `render` is required.
 * This is how the default cell renderer stays type-safe without an `any` cast.
 */
export type BudTableColumn<TRow> =
  | (BudTableColumnBase & {
      key: Extract<keyof TRow, string>;
      render?: (row: TRow) => ReactNode;
    })
  | (BudTableColumnBase & { key: string; render: (row: TRow) => ReactNode });

export interface BudTableProps<TRow> {
  columns: readonly BudTableColumn<TRow>[];
  rows: readonly TRow[];
  rowKey: (row: TRow) => string | number;
  onRowClick?: (row: TRow) => void;
  loading?: boolean;
  emptyMessage?: string;
  size?: 'small' | 'medium';
  ariaLabel?: string;
  sx?: SxProps<Theme>;
}

const renderCell = <TRow,>(column: BudTableColumn<TRow>, row: TRow): ReactNode => {
  if (column.render) return column.render(row);
  // Safe: this branch is only reachable when `key` is a real keyof TRow.
  return String(row[column.key as keyof TRow] ?? '');
};

const BudTable = <TRow,>({
  columns,
  rows,
  rowKey,
  onRowClick,
  loading = false,
  emptyMessage = 'No data',
  size = 'small',
  ariaLabel,
  sx,
}: BudTableProps<TRow>) => (
  <TableContainer sx={{ overflowX: 'auto', ...sx }}>
    <Table size={size} aria-label={ariaLabel}>
      <TableHead>
        <TableRow>
          {columns.map((column) => (
            <TableCell
              key={column.key}
              align={column.align ?? 'left'}
              sx={{ width: column.width, fontWeight: 600, color: 'text.secondary' }}
            >
              {column.header}
            </TableCell>
          ))}
        </TableRow>
      </TableHead>

      <TableBody>
        {loading ? (
          Array.from({ length: 3 }, (_, rowIndex) => (
            <TableRow key={`skeleton-${rowIndex}`}>
              {columns.map((column) => (
                <TableCell key={column.key}>
                  <Skeleton variant="text" />
                </TableCell>
              ))}
            </TableRow>
          ))
        ) : rows.length === 0 ? (
          <TableRow>
            <TableCell colSpan={columns.length} align="center" sx={{ py: 4, border: 0 }}>
              <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                {emptyMessage}
              </Typography>
            </TableCell>
          </TableRow>
        ) : (
          rows.map((row) => (
            <TableRow
              key={rowKey(row)}
              hover={Boolean(onRowClick)}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              sx={onRowClick ? { cursor: 'pointer' } : undefined}
            >
              {columns.map((column) => (
                <TableCell key={column.key} align={column.align ?? 'left'}>
                  {renderCell(column, row)}
                </TableCell>
              ))}
            </TableRow>
          ))
        )}
      </TableBody>
    </Table>
  </TableContainer>
);

export default BudTable;
