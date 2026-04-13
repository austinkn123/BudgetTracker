import { format } from 'date-fns';
import type { Transaction } from '../../../shared/types/api';

type TransactionTableProps = {
  transactions: Transaction[];
};

export function TransactionTable({ transactions }: TransactionTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">ID</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Type</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Date</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Payee</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Account ID</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Category ID</th>
            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Notes</th>
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-gray-200">
          {transactions.length > 0 ? (
            transactions.map((transaction) => (
              <tr key={transaction.id} className="hover:bg-gray-50">
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">{transaction.id}</td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">{transaction.transactionType}</td>
                <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-green-600">${transaction.amount.toFixed(2)}</td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">{format(new Date(transaction.occurredAt), 'PP')}</td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">{transaction.payee || '-'}</td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{transaction.accountId}</td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{transaction.categoryId ?? '-'}</td>
                <td className="px-6 py-4 text-sm text-gray-500 max-w-xs truncate">{transaction.notes || '-'}</td>
              </tr>
            ))
          ) : (
            <tr>
              <td colSpan={8} className="px-6 py-8 text-center text-gray-500 italic">
                No transactions found
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
