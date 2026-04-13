import { Loader2 } from 'lucide-react';

export function DashboardLoadingState() {
  return (
    <div className="flex items-center justify-center py-12">
      <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
      <span className="ml-2 text-gray-600">Loading data from database...</span>
    </div>
  );
}
