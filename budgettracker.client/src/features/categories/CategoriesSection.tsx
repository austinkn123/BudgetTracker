import { useMemo } from 'react';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Typography from '@mui/material/Typography';
import { useCategories } from './hooks/useCategories';

type CategoriesSectionProps = {
  isLoading: boolean;
};

const GROUPS: { label: string; type: string; color: 'success' | 'error' | 'info' }[] = [
  { label: 'Income', type: 'Income', color: 'success' },
  { label: 'Expense', type: 'Expense', color: 'error' },
  { label: 'Both', type: 'Both', color: 'info' },
];

const CategoriesSection = ({ isLoading }: CategoriesSectionProps) => {
  const { data: categories = [] } = useCategories();

  const grouped = useMemo(() => {
    const map = new Map<string, typeof categories>();
    for (const cat of categories) {
      const list = map.get(cat.categoryType) ?? [];
      list.push(cat);
      map.set(cat.categoryType, list);
    }
    return map;
  }, [categories]);

  if (isLoading) return null;

  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="subtitle1" fontWeight={600} className="mb-3">
          Categories
        </Typography>
        {categories.length === 0 ? (
          <Typography color="text.secondary" fontStyle="italic">
            No categories found
          </Typography>
        ) : (
          <div className="space-y-3">
            {GROUPS.map((group) => {
              const items = grouped.get(group.type);
              if (!items || items.length === 0) return null;
              return (
                <div key={group.type}>
                  <Typography variant="caption" color="text.secondary" className="mb-1 block">
                    {group.label}
                  </Typography>
                  <div className="flex flex-wrap gap-1">
                    {items.map((cat) => (
                      <Chip key={cat.id} label={cat.name} size="small" color={group.color} variant="outlined" />
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
};

export default CategoriesSection;
