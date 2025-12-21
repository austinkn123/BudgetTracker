import { useQuery } from '@tanstack/react-query';
import { categoryService, expenseService, userService } from '../services/api.service';
import { format } from 'date-fns';
import { Database, DollarSign, FolderOpen, Loader2, User as UserIcon } from 'lucide-react';

export default function Dashboard() {
  // User ID for now - in production, this would come from authentication
  const userId = 1;

  // Fetch all data using React Query
  const { data: categories = [], isLoading: loadingCategories, error: categoriesError } = useQuery({
    queryKey: ['categories', userId],
    queryFn: () => categoryService.getByUserId(userId),
    retry: 1,
  });

  const { data: expenses = [], isLoading: loadingExpenses, error: expensesError } = useQuery({
    queryKey: ['expenses', userId],
    queryFn: () => expenseService.getByUserId(userId),
    retry: 1,
  });

  const { data: user, isLoading: loadingUser, error: userError } = useQuery({
    queryKey: ['user', userId],
    queryFn: () => userService.getById(userId),
    retry: 1,
  });

  const isLoading = loadingCategories || loadingExpenses || loadingUser;
  const hasErrors = categoriesError || expensesError || userError;

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white shadow">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
          <div className="flex items-center gap-3">
            <Database className="w-8 h-8 text-blue-600" />
            <h1 className="text-3xl font-bold text-gray-900">Budget Tracker Dashboard</h1>
          </div>
          <p className="mt-2 text-sm text-gray-600">View all your budget data in one place</p>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {isLoading && (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
            <span className="ml-2 text-gray-600">Loading data from database...</span>
          </div>
        )}

        {!isLoading && hasErrors && (
          <div className="space-y-8">
            <div className="bg-red-50 border border-red-200 rounded-lg p-4">
              <p className="text-red-800 text-sm">
                <strong>Error:</strong> Unable to connect to the database. Please ensure the database is running and properly configured.
              </p>
              {userError && (
                <p className="text-red-700 text-xs mt-2">User service: {String(userError)}</p>
              )}
              {categoriesError && (
                <p className="text-red-700 text-xs mt-2">Category service: {String(categoriesError)}</p>
              )}
              {expensesError && (
                <p className="text-red-700 text-xs mt-2">Expense service: {String(expensesError)}</p>
              )}
            </div>
          </div>
        )}

        {!isLoading && !hasErrors && (
          <div className="space-y-8">
            {/* Success Banner */}
            <div className="bg-green-50 border border-green-200 rounded-lg p-4">
              <p className="text-green-800 text-sm">
                <strong>Connected:</strong> Successfully connected to the database. Displaying live data.
              </p>
            </div>

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
                      <span className="text-sm font-medium text-gray-500">Cognito User ID:</span>
                      <p className="mt-1 text-gray-900">{user.cognitoUserId}</p>
                    </div>
                    <div>
                      <span className="text-sm font-medium text-gray-500">Created At:</span>
                      <p className="mt-1 text-gray-900">{format(new Date(user.createdAt), 'PPP')}</p>
                    </div>
                  </div>
                ) : (
                  <p className="text-gray-500 italic">No user data found for ID {userId}</p>
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

            {/* Expenses Section */}
            <section className="bg-white rounded-lg shadow">
              <div className="px-6 py-4 border-b border-gray-200">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <DollarSign className="w-5 h-5 text-purple-600" />
                    <h2 className="text-xl font-semibold text-gray-900">Expenses</h2>
                  </div>
                  <span className="text-sm text-gray-500">
                    {expenses?.length || 0} {expenses?.length === 1 ? 'expense' : 'expenses'}
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
                        Amount
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Date
                      </th>
                      <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Merchant
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
                    {expenses && expenses.length > 0 ? (
                      expenses.map((expense) => (
                        <tr key={expense.id} className="hover:bg-gray-50">
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                            {expense.id}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-green-600">
                            ${expense.amount.toFixed(2)}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                            {format(new Date(expense.date), 'PP')}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                            {expense.merchant || '-'}
                          </td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                            {expense.categoryId}
                          </td>
                          <td className="px-6 py-4 text-sm text-gray-500 max-w-xs truncate">
                            {expense.notes || '-'}
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan={6} className="px-6 py-8 text-center text-gray-500 italic">
                          No expenses found
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </section>
          </div>
        )}
      </main>
    </div>
  );
}
