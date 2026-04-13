import { FolderOpen } from 'lucide-react';
import { useCategories } from './hooks/useCategories';

type CategoriesSectionProps = {
  isLoading: boolean;
};

export default function CategoriesSection({ isLoading }: CategoriesSectionProps) {
  const { data: categories = [] } = useCategories();

  if (isLoading) return null;

  return (
    <section className="bg-white rounded-lg shadow">
      <div className="px-6 py-4 border-b border-gray-200">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FolderOpen className="w-5 h-5 text-green-600" />
            <h2 className="text-xl font-semibold text-gray-900">Categories</h2>
          </div>
          <span className="text-sm text-gray-500">
            {categories.length} {categories.length === 1 ? 'category' : 'categories'}
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
            {categories.length > 0 ? (
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
  );
}
