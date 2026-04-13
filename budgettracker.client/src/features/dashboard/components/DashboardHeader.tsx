import { Database } from 'lucide-react';

export function DashboardHeader() {
  return (
    <header className="bg-white shadow">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        <div className="flex items-center gap-3">
          <Database className="w-8 h-8 text-blue-600" />
          <h1 className="text-3xl font-bold text-gray-900">Budget Tracker Dashboard</h1>
        </div>
        <p className="mt-2 text-sm text-gray-600">View all your budget data in one place</p>
      </div>
    </header>
  );
}
