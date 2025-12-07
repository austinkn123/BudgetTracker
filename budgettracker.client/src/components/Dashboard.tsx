import { useQuery } from '@tanstack/react-query';
import { categoryService, expenseService, userService } from '../services/api.service';
import { format } from 'date-fns';
import { Database, DollarSign, FolderOpen, Loader2, User as UserIcon } from 'lucide-react';
import type { User, Category, Expense } from '../types/api';

// Mock data for demonstration when API is not available
const mockUser: User = {
  id: 1,
  cognitoUserId: 'mock-cognito-123',
  email: 'demo@budgettracker.com',
  createdAt: new Date().toISOString(),
};

const mockCategories: Category[] = [
  { id: 1, userId: 1, name: 'Groceries' },
  { id: 2, userId: 1, name: 'Transportation' },
  { id: 3, userId: 1, name: 'Entertainment' },
  { id: 4, userId: 1, name: 'Utilities' },
  { id: 5, userId: 1, name: 'Healthcare' },
];

const mockExpenses: Expense[] = [
  {
    id: 1,
    userId: 1,
    categoryId: 1,
    amount: 85.42,
    date: new Date('2024-12-01').toISOString(),
    merchant: 'Whole Foods',
    notes: 'Weekly grocery shopping',
    createdAt: new Date('2024-12-01').toISOString(),
  },
  {
    id: 2,
    userId: 1,
    categoryId: 2,
    amount: 45.00,
    date: new Date('2024-12-02').toISOString(),
    merchant: 'Uber',
    notes: 'Ride to office',
    createdAt: new Date('2024-12-02').toISOString(),
  },
  {
    id: 3,
    userId: 1,
    categoryId: 3,
    amount: 15.99,
    date: new Date('2024-12-03').toISOString(),
    merchant: 'Netflix',
    notes: 'Monthly subscription',
    createdAt: new Date('2024-12-03').toISOString(),
  },
  {
    id: 4,
    userId: 1,
    categoryId: 4,
    amount: 120.50,
    date: new Date('2024-12-04').toISOString(),
    merchant: 'Electric Company',
    notes: 'November electricity bill',
    createdAt: new Date('2024-12-04').toISOString(),
  },
  {
    id: 5,
    userId: 1,
    categoryId: 1,
    amount: 52.30,
    date: new Date('2024-12-05').toISOString(),
    merchant: "Trader Joe's",
    notes: 'Fresh produce',
    createdAt: new Date('2024-12-05').toISOString(),
  },
];

export default function Dashboard() {
  // Mock user ID for now - in production, this would come from authentication
  const mockUserId = 1;

  // Fetch all data using React Query
  const { data: categories = mockCategories, isLoading: loadingCategories } = useQuery({
    queryKey: ['categories', mockUserId],
    queryFn: () => categoryService.getByUserId(mockUserId),
    retry: 0,
    // Use mock data when fetch fails
    initialData: mockCategories,
  });

  const { data: expenses = mockExpenses, isLoading: loadingExpenses } = useQuery({
    queryKey: ['expenses', mockUserId],
    queryFn: () => expenseService.getByUserId(mockUserId),
    retry: 0,
    // Use mock data when fetch fails
    initialData: mockExpenses,
  });

  const { data: user = mockUser, isLoading: loadingUser } = useQuery({
    queryKey: ['user', mockUserId],
    queryFn: () => userService.getById(mockUserId),
    retry: 0,
    // Use mock data when fetch fails
    initialData: mockUser,
  });

  const isLoading = loadingCategories || loadingExpenses || loadingUser;

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
            <span className="ml-2 text-gray-600">Loading data...</span>
          </div>
        )}

        {!isLoading && (
          <div className="space-y-8">
            {/* Info Banner */}
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
              <p className="text-blue-800 text-sm">
                <strong>Note:</strong> This UI is currently displaying sample data. Connect your backend API with a database to see real data.
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
                  <p className="text-gray-500 italic">No user data found for ID {mockUserId}</p>
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
