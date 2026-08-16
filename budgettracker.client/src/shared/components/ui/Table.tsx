import type { ReactNode } from 'react';
import { cn } from '../../utils/cn';
import Skeleton from './Skeleton';

interface TableColumnBase {
  header: ReactNode;
  width?: string | number;
  align?: 'left' | 'center' | 'right';
}

/**
 * Either `key` names a real field of TRow (so `render` may be omitted and the
 * raw value is printed), or it is a synthetic id and `render` is required.
 * Keeps the default cell renderer type-safe without an `any` cast.
 */
export type TableColumn<TRow> =
  | (TableColumnBase & {
      key: Extract<keyof TRow, string>;
      render?: (row: TRow) => ReactNode;
    })
  | (TableColumnBase & { key: string; render: (row: TRow) => ReactNode });

export interface TableProps<TRow> {
  columns: readonly TableColumn<TRow>[];
  rows: readonly TRow[];
  rowKey: (row: TRow) => string | number;
  onRowClick?: (row: TRow) => void;
  loading?: boolean;
  emptyMessage?: string;
  size?: 'small' | 'medium';
  ariaLabel?: string;
  className?: string;
}

const ALIGN_CLASSES = { left: 'text-left', center: 'text-center', right: 'text-right' } as const;

const renderCell = <TRow,>(column: TableColumn<TRow>, row: TRow): ReactNode => {
  if (column.render) return column.render(row);
  // Safe: this branch is only reachable when `key` is a real keyof TRow.
  return String(row[column.key as keyof TRow] ?? '');
};

/** Data table (BUD-20): semantic markup, subtle row rules, compact density. */
const Table = <TRow,>({
  columns,
  rows,
  rowKey,
  onRowClick,
  loading = false,
  emptyMessage = 'No data',
  size = 'small',
  ariaLabel,
  className,
}: TableProps<TRow>) => {
  const cellPadding = size === 'small' ? 'px-4 py-3' : 'px-4 py-4';

  return (
    <div className={cn('-mx-6 overflow-x-auto', className)}>
      <table aria-label={ariaLabel} className="w-full border-collapse text-sm tabular-nums">
        <thead>
          <tr className="bg-background/60">
            {columns.map((column) => (
              <th
                key={column.key}
                scope="col"
                style={{ width: column.width }}
                className={cn(
                  'px-4 py-2.5',
                  'whitespace-nowrap border-y border-border-subtle text-2xs font-semibold uppercase tracking-[0.07em] text-ink-muted',
                  ALIGN_CLASSES[column.align ?? 'left'],
                )}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>

        <tbody>
          {loading ? (
            Array.from({ length: 3 }, (_, rowIndex) => (
              <tr key={`skeleton-${rowIndex}`} className="border-b border-border-subtle">
                {columns.map((column) => (
                  <td key={column.key} className={cellPadding}>
                    <Skeleton />
                  </td>
                ))}
              </tr>
            ))
          ) : rows.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="py-10 text-center">
                <p className="text-sm text-ink-muted">{emptyMessage}</p>
              </td>
            </tr>
          ) : (
            rows.map((row) => (
              <tr
                key={rowKey(row)}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                className={cn(
                  'border-b border-border-subtle last:border-b-0',
                  onRowClick && 'cursor-pointer transition-colors duration-120 hover:bg-primary/[0.03]',
                )}
              >
                {columns.map((column) => (
                  <td
                    key={column.key}
                    className={cn(cellPadding, 'text-ink', ALIGN_CLASSES[column.align ?? 'left'])}
                  >
                    {renderCell(column, row)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
};

export default Table;
