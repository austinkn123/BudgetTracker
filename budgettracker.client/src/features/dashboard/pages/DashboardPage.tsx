import { format } from 'date-fns';
import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Database, DollarSign, FolderOpen, User as UserIcon } from 'lucide-react';
import { useUser } from '../../../lib/useUser';
import { useCategories } from '../../../lib/useCategories';
import { useTransactions } from '../../../lib/useTransactions';
import { useBudgetPlans } from '../../../lib/useBudgetPlans';
import { transactionService } from '../../../services/transaction.service';
import { budgetPlanService } from '../../../services/budgetPlan.service';
import type { BudgetPlan, BudgetPlanLine, Transaction } from '../../../types/api';
import { DashboardHeader } from '../components/DashboardHeader';
import { DashboardLoadingState } from '../components/DashboardLoadingState';
import { DashboardErrorState } from '../components/DashboardErrorState';
import { DashboardStatusBanner } from '../components/DashboardStatusBanner';

type ExpenseFormState = {
  accountId: number;
  categoryId: number;
  amount: number;
  occurredAt: string;
  payee: string;
  notes: string;
};

type PlanExpenseFormState = {
  categoryId: number;
  bucket: 'Core' | 'Buffer';
  cadence: 'Monthly' | 'Annual';
  amount: number;
  isStressFactor: boolean;
  notes: string;
};

const todayInputValue = format(new Date(), 'yyyy-MM-dd');

export default function DashboardPage() {
  const queryClient = useQueryClient();

  // Fetch all data using custom hooks
  const { data: user, isLoading: loadingUser, error: userError } = useUser();
  const { data: categories = [], isLoading: loadingCategories, error: categoriesError } = useCategories();
  const { data: transactions = [], isLoading: loadingTransactions, error: transactionsError } = useTransactions();
  const { data: budgetPlans = [], isLoading: loadingBudgetPlans, error: budgetPlansError } = useBudgetPlans();

  const expenseCategories = useMemo(
    () => categories.filter((category) => category.categoryType === 'Expense' || category.categoryType === 'Both'),
    [categories],
  );

  const expenseTransactions = useMemo(
    () => transactions.filter((transaction) => transaction.transactionType === 'Expense'),
    [transactions],
  );

  const categoryNameById = useMemo(
    () => new Map(categories.map((category) => [category.id, category.name])),
    [categories],
  );

  const getExpenseLineOptionLabel = (line: BudgetPlanLine) => {
    const categoryName = line.categoryId
      ? (categoryNameById.get(line.categoryId) ?? `Unknown (${line.categoryId})`)
      : 'No Category';

    return `${categoryName} | ${line.bucket} ${line.cadence} | $${line.amount.toFixed(2)} ($${line.monthlyEquivalent.toFixed(2)}/mo)`;
  };

  const expensePlanLines = useMemo(
    () => budgetPlans.flatMap((plan) =>
      plan.lines
        .filter((line) => line.lineType === 'Expense')
        .map((line) => ({ planId: plan.id, line })),
    ),
    [budgetPlans],
  );

  const defaultAccountId = transactions[0]?.accountId ?? 1;

  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);

  const [newExpense, setNewExpense] = useState<ExpenseFormState>({
    accountId: defaultAccountId,
    categoryId: 0,
    amount: 0,
    occurredAt: todayInputValue,
    payee: '',
    notes: '',
  });

  const [editExpenseId, setEditExpenseId] = useState<number | ''>('');
  const [editExpense, setEditExpense] = useState<ExpenseFormState>({
    accountId: defaultAccountId,
    categoryId: 0,
    amount: 0,
    occurredAt: todayInputValue,
    payee: '',
    notes: '',
  });

  const [newPlanExpensePlanId, setNewPlanExpensePlanId] = useState<number | ''>('');
  const [newPlanExpense, setNewPlanExpense] = useState<PlanExpenseFormState>({
    categoryId: 0,
    bucket: 'Core',
    cadence: 'Monthly',
    amount: 0,
    isStressFactor: false,
    notes: '',
  });

  const [editPlanExpensePlanId, setEditPlanExpensePlanId] = useState<number | ''>('');
  const [editPlanExpenseLineId, setEditPlanExpenseLineId] = useState<number | ''>('');
  const [editPlanExpense, setEditPlanExpense] = useState<PlanExpenseFormState>({
    categoryId: 0,
    bucket: 'Core',
    cadence: 'Monthly',
    amount: 0,
    isStressFactor: false,
    notes: '',
  });

  useEffect(() => {
    if (newExpense.accountId <= 0) {
      setNewExpense((prev) => ({ ...prev, accountId: defaultAccountId }));
    }

    if (editExpense.accountId <= 0) {
      setEditExpense((prev) => ({ ...prev, accountId: defaultAccountId }));
    }
  }, [defaultAccountId, editExpense.accountId, newExpense.accountId]);

  useEffect(() => {
    if (expenseCategories.length === 0) {
      return;
    }

    if (newExpense.categoryId <= 0) {
      setNewExpense((prev) => ({ ...prev, categoryId: expenseCategories[0].id }));
    }

    if (editExpense.categoryId <= 0) {
      setEditExpense((prev) => ({ ...prev, categoryId: expenseCategories[0].id }));
    }

    if (newPlanExpense.categoryId <= 0) {
      setNewPlanExpense((prev) => ({ ...prev, categoryId: expenseCategories[0].id }));
    }

    if (editPlanExpense.categoryId <= 0) {
      setEditPlanExpense((prev) => ({ ...prev, categoryId: expenseCategories[0].id }));
    }
  }, [editExpense.categoryId, editPlanExpense.categoryId, expenseCategories, newExpense.categoryId, newPlanExpense.categoryId]);

  const createExpenseMutation = useMutation({
    mutationFn: async (payload: Omit<Transaction, 'id'>) => transactionService.create(payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['transactions'] });
      setStatusError(null);
      setStatusMessage('Expense transaction added.');
      setNewExpense((prev) => ({ ...prev, amount: 0, payee: '', notes: '' }));
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to add expense transaction: ${error.message}`);
    },
  });

  const updateExpenseMutation = useMutation({
    mutationFn: async ({ id, payload }: { id: number; payload: Transaction }) => transactionService.update(id, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['transactions'] });
      setStatusError(null);
      setStatusMessage('Expense transaction updated.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to update expense transaction: ${error.message}`);
    },
  });

  const updateBudgetPlanMutation = useMutation({
    mutationFn: async ({ id, payload }: { id: number; payload: BudgetPlan }) => budgetPlanService.update(id, payload),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['budgetPlans'] });
      setStatusError(null);
      setStatusMessage('Budget plan expense updated.');
    },
    onError: (error: Error) => {
      setStatusMessage(null);
      setStatusError(`Unable to update budget plan expense: ${error.message}`);
    },
  });

  const saveNewExpense = () => {
    if (newExpense.accountId <= 0 || newExpense.categoryId <= 0 || newExpense.amount <= 0) {
      setStatusMessage(null);
      setStatusError('Account, category, and amount are required to add an expense transaction.');
      return;
    }

    const payload: Omit<Transaction, 'id'> = {
      accountId: newExpense.accountId,
      categoryId: newExpense.categoryId,
      transactionType: 'Expense',
      amount: newExpense.amount,
      occurredAt: new Date(`${newExpense.occurredAt}T00:00:00`).toISOString(),
      payee: newExpense.payee || undefined,
      notes: newExpense.notes || undefined,
      transferAccountId: undefined,
      createdAt: new Date().toISOString(),
    };

    createExpenseMutation.mutate(payload);
  };

  const loadExpenseForEdit = (id: number | '') => {
    setEditExpenseId(id);
    if (id === '') {
      return;
    }

    const transaction = expenseTransactions.find((item) => item.id === id);
    if (!transaction) {
      return;
    }

    setEditExpense({
      accountId: transaction.accountId,
      categoryId: transaction.categoryId,
      amount: transaction.amount,
      occurredAt: format(new Date(transaction.occurredAt), 'yyyy-MM-dd'),
      payee: transaction.payee || '',
      notes: transaction.notes || '',
    });
  };

  const saveEditedExpense = () => {
    if (editExpenseId === '') {
      setStatusMessage(null);
      setStatusError('Select an expense transaction to update.');
      return;
    }

    if (editExpense.accountId <= 0 || editExpense.categoryId <= 0 || editExpense.amount <= 0) {
      setStatusMessage(null);
      setStatusError('Account, category, and amount are required to update an expense transaction.');
      return;
    }

    const existing = expenseTransactions.find((item) => item.id === editExpenseId);
    if (!existing) {
      setStatusMessage(null);
      setStatusError('Selected expense transaction was not found.');
      return;
    }

    const payload: Transaction = {
      ...existing,
      accountId: editExpense.accountId,
      categoryId: editExpense.categoryId,
      amount: editExpense.amount,
      occurredAt: new Date(`${editExpense.occurredAt}T00:00:00`).toISOString(),
      payee: editExpense.payee || undefined,
      notes: editExpense.notes || undefined,
      transactionType: 'Expense',
      transferAccountId: undefined,
    };

    updateExpenseMutation.mutate({ id: editExpenseId, payload });
  };

  const addBudgetPlanExpenseLine = () => {
    if (newPlanExpensePlanId === '') {
      setStatusMessage(null);
      setStatusError('Select a budget plan before adding a planned expense line.');
      return;
    }

    if (newPlanExpense.categoryId <= 0 || newPlanExpense.amount <= 0) {
      setStatusMessage(null);
      setStatusError('Category and amount are required for a planned expense line.');
      return;
    }

    const plan = budgetPlans.find((item) => item.id === newPlanExpensePlanId);
    if (!plan) {
      setStatusMessage(null);
      setStatusError('Selected budget plan was not found.');
      return;
    }

    const maxSortOrder = plan.lines.length > 0
      ? Math.max(...plan.lines.map((line) => line.sortOrder))
      : 0;

    const newLine: BudgetPlanLine = {
      id: 0,
      budgetPlanId: plan.id,
      categoryId: newPlanExpense.categoryId,
      lineType: 'Expense',
      bucket: newPlanExpense.bucket,
      cadence: newPlanExpense.cadence,
      amount: newPlanExpense.amount,
      monthlyEquivalent: newPlanExpense.cadence === 'Annual'
        ? Number((newPlanExpense.amount / 12).toFixed(2))
        : newPlanExpense.amount,
      isStressFactor: newPlanExpense.isStressFactor,
      notes: newPlanExpense.notes || null,
      sortOrder: maxSortOrder + 10,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    const payload: BudgetPlan = {
      ...plan,
      lines: [...plan.lines, newLine],
    };

    updateBudgetPlanMutation.mutate({ id: plan.id, payload });
  };

  const onEditPlanSelection = (planId: number | '') => {
    setEditPlanExpensePlanId(planId);
    setEditPlanExpenseLineId('');

    if (planId === '') {
      return;
    }

    const firstLine = budgetPlans
      .find((plan) => plan.id === planId)
      ?.lines
      .find((line) => line.lineType === 'Expense');

    if (!firstLine) {
      return;
    }

    setEditPlanExpenseLineId(firstLine.id);
    setEditPlanExpense({
      categoryId: firstLine.categoryId ?? 0,
      bucket: firstLine.bucket,
      cadence: firstLine.cadence,
      amount: firstLine.amount,
      isStressFactor: firstLine.isStressFactor,
      notes: firstLine.notes || '',
    });
  };

  const onEditPlanLineSelection = (lineId: number | '') => {
    setEditPlanExpenseLineId(lineId);
    if (lineId === '' || editPlanExpensePlanId === '') {
      return;
    }

    const line = budgetPlans
      .find((plan) => plan.id === editPlanExpensePlanId)
      ?.lines
      .find((item) => item.id === lineId && item.lineType === 'Expense');

    if (!line) {
      return;
    }

    setEditPlanExpense({
      categoryId: line.categoryId ?? 0,
      bucket: line.bucket,
      cadence: line.cadence,
      amount: line.amount,
      isStressFactor: line.isStressFactor,
      notes: line.notes || '',
    });
  };

  const saveEditedBudgetPlanExpenseLine = () => {
    if (editPlanExpensePlanId === '' || editPlanExpenseLineId === '') {
      setStatusMessage(null);
      setStatusError('Select a budget plan and expense line to update.');
      return;
    }

    if (editPlanExpense.categoryId <= 0 || editPlanExpense.amount <= 0) {
      setStatusMessage(null);
      setStatusError('Category and amount are required to update a planned expense line.');
      return;
    }

    const plan = budgetPlans.find((item) => item.id === editPlanExpensePlanId);
    if (!plan) {
      setStatusMessage(null);
      setStatusError('Selected budget plan was not found.');
      return;
    }

    const lineExists = plan.lines.some((line) => line.id === editPlanExpenseLineId && line.lineType === 'Expense');
    if (!lineExists) {
      setStatusMessage(null);
      setStatusError('Selected budget expense line was not found.');
      return;
    }

    const payload: BudgetPlan = {
      ...plan,
      lines: plan.lines.map((line) => {
        if (line.id !== editPlanExpenseLineId) {
          return line;
        }

        return {
          ...line,
          categoryId: editPlanExpense.categoryId,
          bucket: editPlanExpense.bucket,
          cadence: editPlanExpense.cadence,
          amount: editPlanExpense.amount,
          monthlyEquivalent: editPlanExpense.cadence === 'Annual'
            ? Number((editPlanExpense.amount / 12).toFixed(2))
            : editPlanExpense.amount,
          isStressFactor: editPlanExpense.isStressFactor,
          notes: editPlanExpense.notes || null,
          updatedAt: new Date().toISOString(),
        };
      }),
    };

    updateBudgetPlanMutation.mutate({ id: plan.id, payload });
  };

  const isLoading = loadingCategories || loadingTransactions || loadingUser || loadingBudgetPlans;
  const hasErrors = categoriesError || transactionsError || userError || budgetPlansError;
  const isSavingExpense = createExpenseMutation.isPending || updateExpenseMutation.isPending;
  const isSavingPlan = updateBudgetPlanMutation.isPending;

  return (
    <div className="min-h-screen bg-gray-50">
      <DashboardHeader />

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {isLoading && <DashboardLoadingState />}

        {!isLoading && hasErrors && (
          <DashboardErrorState
            userError={userError}
            categoriesError={categoriesError}
            transactionsError={transactionsError}
            budgetPlansError={budgetPlansError}
          />
        )}

        {!isLoading && !hasErrors && (
          <div className="space-y-8">
            <DashboardStatusBanner statusMessage={statusMessage} statusError={statusError} />

            {/* User Section */}
            <section className="bg-white rounded-lg shadow">
              <div className="px-6 py-4 border-b border-gray-200">
                <div className="flex items-center gap-2">
                  <UserIcon className="w-5 h-5 text-blue-600" />
                  <h2 className="text-xl font-semibold text-gray-900">User Information</h2>
                </div>
              </div>
              <div className="px-6 py-4">
                {user ? (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <span className="text-sm font-medium text-gray-500">User ID:</span>
                      <p className="mt-1 text-gray-900">{user.id}</p>
                    </div>
                    <div>
                      <span className="text-sm font-medium text-gray-500">Email:</span>
                      <p className="mt-1 text-gray-900">{user.email}</p>
                    </div>
                    <div>
                      <span className="text-sm font-medium text-gray-500">Created At:</span>
                      <p className="mt-1 text-gray-900">{format(new Date(user.createdAt), 'PPP')}</p>
                    </div>
                  </div>
                ) : (
                  <p className="text-gray-500 italic">No user data found</p>
                )}
              </div>
            </section>

            {/* Categories Section */}
            <section className="bg-white rounded-lg shadow">
              <div className="px-6 py-4 border-b border-gray-200">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <FolderOpen className="w-5 h-5 text-green-600" />
                    <h2 className="text-xl font-semibold text-gray-900">Categories</h2>
                  </div>
                  <span className="text-sm text-gray-500">
                    {categories?.length || 0} {categories?.length === 1 ? 'category' : 'categories'}
                  </span>
                </div>
              </div>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200">
                  <thead className="bg-gray-50">
                    <tr>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        ID
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Name
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        User ID
                      </th>
                    </tr>
                  </thead>
                  <tbody className="bg-white divide-y divide-gray-200">
                    {categories && categories.length > 0 ? (
                      categories.map((category) => (
                        <tr key={category.id} className="hover:bg-gray-50">
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                            {category.id}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
                            {category.name}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                            {category.userId}
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan={3} className="px-6 py-8 text-center text-gray-500 italic">
                          No categories found
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </section>

            {/* Transactions Section */}
            <section className="bg-white rounded-lg shadow">
              <div className="px-6 py-4 border-b border-gray-200">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <DollarSign className="w-5 h-5 text-purple-600" />
                    <h2 className="text-xl font-semibold text-gray-900">Transactions</h2>
                  </div>
                  <span className="text-sm text-gray-500">
                    {transactions?.length || 0} {transactions?.length === 1 ? 'transaction' : 'transactions'}
                  </span>
                </div>
              </div>

              <div className="px-6 py-4 border-b border-gray-200 space-y-6 bg-gray-50">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900 mb-3">Add Expense Transaction</h3>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <input
                      type="number"
                      min={1}
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      placeholder="Account ID"
                      value={newExpense.accountId}
                      onChange={(e) => setNewExpense((prev) => ({ ...prev, accountId: Number(e.target.value) }))}
                    />
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={newExpense.categoryId}
                      onChange={(e) => setNewExpense((prev) => ({ ...prev, categoryId: Number(e.target.value) }))}
                    >
                      {expenseCategories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                    <input
                      type="number"
                      step="0.01"
                      min={0}
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      placeholder="Amount"
                      value={newExpense.amount}
                      onChange={(e) => setNewExpense((prev) => ({ ...prev, amount: Number(e.target.value) }))}
                    />
                    <input
                      type="date"
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={newExpense.occurredAt}
                      onChange={(e) => setNewExpense((prev) => ({ ...prev, occurredAt: e.target.value }))}
                    />
                    <input
                      type="text"
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      placeholder="Payee"
                      value={newExpense.payee}
                      onChange={(e) => setNewExpense((prev) => ({ ...prev, payee: e.target.value }))}
                    />
                    <input
                      type="text"
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      placeholder="Notes"
                      value={newExpense.notes}
                      onChange={(e) => setNewExpense((prev) => ({ ...prev, notes: e.target.value }))}
                    />
                  </div>
                  <div className="mt-3">
                    <button
                      type="button"
                      className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
                      onClick={saveNewExpense}
                      disabled={isSavingExpense}
                    >
                      {createExpenseMutation.isPending ? 'Saving...' : 'Add Expense'}
                    </button>
                  </div>
                </div>

                <div>
                  <h3 className="text-sm font-semibold text-gray-900 mb-3">Update Expense Transaction</h3>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={editExpenseId}
                      onChange={(e) => loadExpenseForEdit(e.target.value ? Number(e.target.value) : '')}
                    >
                      <option value="">Select expense transaction</option>
                      {expenseTransactions.map((transaction) => (
                        <option key={transaction.id} value={transaction.id}>
                          #{transaction.id} - ${transaction.amount.toFixed(2)} - {transaction.payee || 'No payee'}
                        </option>
                      ))}
                    </select>
                    <input
                      type="number"
                      min={1}
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      placeholder="Account ID"
                      value={editExpense.accountId}
                      onChange={(e) => setEditExpense((prev) => ({ ...prev, accountId: Number(e.target.value) }))}
                    />
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={editExpense.categoryId}
                      onChange={(e) => setEditExpense((prev) => ({ ...prev, categoryId: Number(e.target.value) }))}
                    >
                      {expenseCategories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                    <input
                      type="number"
                      step="0.01"
                      min={0}
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      placeholder="Amount"
                      value={editExpense.amount}
                      onChange={(e) => setEditExpense((prev) => ({ ...prev, amount: Number(e.target.value) }))}
                    />
                    <input
                      type="date"
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={editExpense.occurredAt}
                      onChange={(e) => setEditExpense((prev) => ({ ...prev, occurredAt: e.target.value }))}
                    />
                    <input
                      type="text"
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      placeholder="Payee"
                      value={editExpense.payee}
                      onChange={(e) => setEditExpense((prev) => ({ ...prev, payee: e.target.value }))}
                    />
                    <input
                      type="text"
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm md:col-span-3"
                      placeholder="Notes"
                      value={editExpense.notes}
                      onChange={(e) => setEditExpense((prev) => ({ ...prev, notes: e.target.value }))}
                    />
                  </div>
                  <div className="mt-3">
                    <button
                      type="button"
                      className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
                      onClick={saveEditedExpense}
                      disabled={isSavingExpense}
                    >
                      {updateExpenseMutation.isPending ? 'Saving...' : 'Update Expense'}
                    </button>
                  </div>
                </div>
              </div>

              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200">
                  <thead className="bg-gray-50">
                    <tr>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        ID
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Type
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Amount
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Date
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Payee
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Account ID
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Category ID
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Notes
                      </th>
                    </tr>
                  </thead>
                  <tbody className="bg-white divide-y divide-gray-200">
                    {transactions && transactions.length > 0 ? (
                      transactions.map((transaction) => (
                        <tr key={transaction.id} className="hover:bg-gray-50">
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                            {transaction.id}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                            {transaction.transactionType}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-green-600">
                            ${transaction.amount.toFixed(2)}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                            {format(new Date(transaction.occurredAt), 'PP')}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                            {transaction.payee || '-'}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                            {transaction.accountId}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                            {transaction.categoryId ?? '-'}
                          </td>
                          <td className="px-6 py-4 text-sm text-gray-500 max-w-xs truncate">
                            {transaction.notes || '-'}
                          </td>
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
            </section>

            {/* Budget Plans Section */}
            <section className="bg-white rounded-lg shadow">
              <div className="px-6 py-4 border-b border-gray-200">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Database className="w-5 h-5 text-indigo-600" />
                    <h2 className="text-xl font-semibold text-gray-900">Budget Plans</h2>
                  </div>
                  <span className="text-sm text-gray-500">
                    {budgetPlans?.length || 0} {budgetPlans?.length === 1 ? 'plan' : 'plans'}
                  </span>
                </div>
              </div>

              <div className="px-6 py-4 border-b border-gray-200 space-y-6 bg-gray-50">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900 mb-3">Add Planned Expense Line</h3>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={newPlanExpensePlanId}
                      onChange={(e) => setNewPlanExpensePlanId(e.target.value ? Number(e.target.value) : '')}
                    >
                      <option value="">Select budget plan</option>
                      {budgetPlans.map((plan) => (
                        <option key={plan.id} value={plan.id}>
                          {plan.name} ({format(new Date(plan.planMonth), 'MMM yyyy')})
                        </option>
                      ))}
                    </select>
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={newPlanExpense.categoryId}
                      onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, categoryId: Number(e.target.value) }))}
                    >
                      {expenseCategories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                    <input
                      type="number"
                      step="0.01"
                      min={0}
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      placeholder="Amount"
                      value={newPlanExpense.amount}
                      onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, amount: Number(e.target.value) }))}
                    />
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={newPlanExpense.bucket}
                      onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, bucket: e.target.value as 'Core' | 'Buffer' }))}
                    >
                      <option value="Core">Core</option>
                      <option value="Buffer">Buffer</option>
                    </select>
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={newPlanExpense.cadence}
                      onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, cadence: e.target.value as 'Monthly' | 'Annual' }))}
                    >
                      <option value="Monthly">Monthly</option>
                      <option value="Annual">Annual</option>
                    </select>
                    <label className="inline-flex items-center gap-2 rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700">
                      <input
                        type="checkbox"
                        checked={newPlanExpense.isStressFactor}
                        onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, isStressFactor: e.target.checked }))}
                      />
                      Stress Factor
                    </label>
                    <input
                      type="text"
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm md:col-span-3"
                      placeholder="Notes"
                      value={newPlanExpense.notes}
                      onChange={(e) => setNewPlanExpense((prev) => ({ ...prev, notes: e.target.value }))}
                    />
                  </div>
                  <div className="mt-3">
                    <button
                      type="button"
                      className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
                      onClick={addBudgetPlanExpenseLine}
                      disabled={isSavingPlan}
                    >
                      {isSavingPlan ? 'Saving...' : 'Add Planned Expense'}
                    </button>
                  </div>
                </div>

                <div>
                  <h3 className="text-sm font-semibold text-gray-900 mb-3">Update Planned Expense Line</h3>
                  <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={editPlanExpensePlanId}
                      onChange={(e) => onEditPlanSelection(e.target.value ? Number(e.target.value) : '')}
                    >
                      <option value="">Select budget plan</option>
                      {budgetPlans.map((plan) => (
                        <option key={plan.id} value={plan.id}>
                          {plan.name} ({format(new Date(plan.planMonth), 'MMM yyyy')})
                        </option>
                      ))}
                    </select>
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={editPlanExpenseLineId}
                      onChange={(e) => onEditPlanLineSelection(e.target.value ? Number(e.target.value) : '')}
                    >
                      <option value="">Select expense line</option>
                      {expensePlanLines
                        .filter((item) => editPlanExpensePlanId === '' || item.planId === editPlanExpensePlanId)
                        .map((item) => (
                          <option key={item.line.id} value={item.line.id}>
                            {getExpenseLineOptionLabel(item.line)}
                          </option>
                        ))}
                    </select>
                    <input
                      type="number"
                      step="0.01"
                      min={0}
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      placeholder="Amount"
                      value={editPlanExpense.amount}
                      onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, amount: Number(e.target.value) }))}
                    />
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={editPlanExpense.bucket}
                      onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, bucket: e.target.value as 'Core' | 'Buffer' }))}
                    >
                      <option value="Core">Core</option>
                      <option value="Buffer">Buffer</option>
                    </select>
                    <select
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm"
                      value={editPlanExpense.cadence}
                      onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, cadence: e.target.value as 'Monthly' | 'Annual' }))}
                    >
                      <option value="Monthly">Monthly</option>
                      <option value="Annual">Annual</option>
                    </select>
                    <label className="inline-flex items-center gap-2 rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700">
                      <input
                        type="checkbox"
                        checked={editPlanExpense.isStressFactor}
                        onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, isStressFactor: e.target.checked }))}
                      />
                      Stress Factor
                    </label>
                    <input
                      type="text"
                      className="rounded-md border border-gray-300 px-3 py-2 text-sm md:col-span-3"
                      placeholder="Notes"
                      value={editPlanExpense.notes}
                      onChange={(e) => setEditPlanExpense((prev) => ({ ...prev, notes: e.target.value }))}
                    />
                  </div>
                  <div className="mt-3">
                    <button
                      type="button"
                      className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
                      onClick={saveEditedBudgetPlanExpenseLine}
                      disabled={isSavingPlan}
                    >
                      {isSavingPlan ? 'Saving...' : 'Update Planned Expense'}
                    </button>
                  </div>
                </div>
              </div>

              <div className="px-6 py-4 space-y-6">
                {budgetPlans && budgetPlans.length > 0 ? (
                  budgetPlans.map((plan) => {
                    const monthlyIncome = plan.lines
                      .filter((line) => line.lineType === 'Income')
                      .reduce((sum, line) => sum + line.monthlyEquivalent, 0);

                    const monthlyExpenses = plan.lines
                      .filter((line) => line.lineType === 'Expense')
                      .reduce((sum, line) => sum + line.monthlyEquivalent, 0);

                    const monthlyNet = monthlyIncome - monthlyExpenses;

                    return (
                      <div key={plan.id} className="border border-gray-200 rounded-lg">
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
                  })
                ) : (
                  <p className="text-gray-500 italic">No budget plans found</p>
                )}
              </div>
            </section>
          </div>
        )}
      </main>
    </div>
  );
}
