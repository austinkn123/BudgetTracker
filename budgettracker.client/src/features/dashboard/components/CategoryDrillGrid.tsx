import { useState } from 'react';
import Card from '../../../shared/components/ui/Card';
import type { CategoryCard } from '../utils/selectors';
import CategoryDrillCard from './CategoryDrillCard';

interface CategoryDrillGridProps {
  cards: CategoryCard[];
}

const CategoryDrillGrid = ({ cards }: CategoryDrillGridProps) => {
  const [expandedKey, setExpandedKey] = useState<string | null>(null);

  if (cards.length === 0) {
    return (
      <Card title="Category Drill-Down">
        <p className="text-sm text-ink-muted">
          Add expense line items to your plan to drill into category performance.
        </p>
      </Card>
    );
  }

  return (
    <div>
      <h2 className="mb-4 text-base font-semibold text-ink">Category Drill-Down</h2>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {cards.map((c) => (
          <CategoryDrillCard
            key={c.key}
            data={c}
            expanded={expandedKey === c.key}
            onToggle={() => setExpandedKey((prev) => (prev === c.key ? null : c.key))}
          />
        ))}
      </div>
    </div>
  );
};

export default CategoryDrillGrid;
