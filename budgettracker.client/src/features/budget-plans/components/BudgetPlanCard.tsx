import { format } from 'date-fns';
import type { BudgetPlan } from '../../../shared/types/api';

type BudgetPlanCardProps = {
  plan: BudgetPlan;
  categoryNameById: Map<number, string>;
};

export function BudgetPlanCard({ plan, categoryNameById }: BudgetPlanCardProps) {
  const monthlyIncome = plan.lines
    .filter((line) => line.lineType === 'Income')
    .reduce((sum, line) => sum + line.monthlyEquivalent, 0);

  const monthlyExpenses = plan.lines
    .filter((line) => line.lineType === 'Expense')
    .reduce((sum, line) => sum + line.monthlyEquivalent, 0);

  const monthlyNet = monthlyIncome - monthlyExpenses;

  return (
    <div className="border border-gray-200 rounded-lg">
      <div className="px-4 py-3 bg-gray-50 border-b border-gray-200">
        <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
          <div>
            <p className="text-base font-semibold text-gray-900">{plan.name}</p>
            <p className="text-sm text-gray-600">
              {format(new Date(plan.planMonth), 'MMMM yyyy')} {plan.isActive ? '• Active' : '• Inactive'}
            </p>
          </div>
          <div className="text-sm text-gray-700">
            <span className="font-medium">Target Income:</span> ${plan.netIncomeMonthly.toFixed(2)}
          </div>
        </div>
      </div>

      <div className="px-4 py-3 grid grid-cols-1 md:grid-cols-3 gap-3 text-sm">
        <div className="rounded-md bg-green-50 px-3 py-2">
          <p className="text-green-700">Monthly Income</p>
          <p className="text-green-900 font-semibold">${monthlyIncome.toFixed(2)}</p>
        </div>
        <div className="rounded-md bg-red-50 px-3 py-2">
          <p className="text-red-700">Monthly Expenses</p>
          <p className="text-red-900 font-semibold">${monthlyExpenses.toFixed(2)}</p>
        </div>
        <div className="rounded-md bg-blue-50 px-3 py-2">
          <p className="text-blue-700">Monthly Net</p>
          <p className="text-blue-900 font-semibold">${monthlyNet.toFixed(2)}</p>
        </div>
      </div>

      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Line</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Type</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bucket</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Cadence</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Monthly Eq.</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Category</th>
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {plan.lines.length > 0 ? (
              plan.lines
                .slice()
                .sort((a, b) => a.sortOrder - b.sortOrder)
                .map((line) => (
                  <tr key={line.id} className="hover:bg-gray-50">
                    <td className="px-4 py-2 text-sm text-gray-900">{line.id}</td>
                    <td className="px-4 py-2 text-sm text-gray-900">{line.lineType}</td>
                    <td className="px-4 py-2 text-sm text-gray-900">{line.bucket}</td>
                    <td className="px-4 py-2 text-sm text-gray-900">{line.cadence}</td>
                    <td className="px-4 py-2 text-sm text-gray-900">${line.amount.toFixed(2)}</td>
                    <td className="px-4 py-2 text-sm text-gray-900">${line.monthlyEquivalent.toFixed(2)}</td>
                    <td className="px-4 py-2 text-sm text-gray-500">
                      {line.categoryId ? (categoryNameById.get(line.categoryId) ?? `Unknown (${line.categoryId})`) : '-'}
                    </td>
                  </tr>
                ))
            ) : (
              <tr>
                <td colSpan={7} className="px-4 py-6 text-center text-gray-500 italic">
                  No plan lines found
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
