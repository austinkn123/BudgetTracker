import { useState } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import BudCard from '../../../shared/components/ui/BudCard';
import type { CategoryCard } from '../utils/selectors';
import CategoryDrillCard from './CategoryDrillCard';

interface CategoryDrillGridProps {
  cards: CategoryCard[];
}

const CategoryDrillGrid = ({ cards }: CategoryDrillGridProps) => {
  const [expandedKey, setExpandedKey] = useState<string | null>(null);

  if (cards.length === 0) {
    return (
      <BudCard title="Category Drill-Down">
        <Typography variant="body2" color="text.secondary">
          Add expense line items to your plan to drill into category performance.
        </Typography>
      </BudCard>
    );
  }

  return (
    <Box>
      <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
        Category Drill-Down
      </Typography>
      <Box className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {cards.map((c) => (
          <CategoryDrillCard
            key={c.key}
            data={c}
            expanded={expandedKey === c.key}
            onToggle={() => setExpandedKey((prev) => (prev === c.key ? null : c.key))}
          />
        ))}
      </Box>
    </Box>
  );
};

export default CategoryDrillGrid;
