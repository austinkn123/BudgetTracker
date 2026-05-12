import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import { alpha, useTheme } from '@mui/material/styles';
import { useMemo } from 'react';
import type { BudgetPlan, Transaction } from '../../../shared/types/api';
import { aggregateByBucket } from '../utils/planMath';
import { getSemanticColors } from '../utils/chartTheme';

interface BucketBreakdownProps {
  plan: BudgetPlan | undefined;
  transactions: Transaction[];
}

const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

export function BucketBreakdown({ plan, transactions }: BucketBreakdownProps) {
  const theme = useTheme();
  const semantic = getSemanticColors(theme);

  const rows = useMemo(() => aggregateByBucket(plan, transactions), [plan, transactions]);

  if (!plan || rows.every((r) => r.planned === 0 && r.actual === 0)) {
    return (
      <Card className="h-full">
        <CardContent className="flex flex-col items-center justify-center h-full min-h-[200px]">
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 1 }}>
            Bucket Breakdown
          </Typography>
          <Typography variant="body2" color="text.secondary">
            No bucket data for this plan yet
          </Typography>
        </CardContent>
      </Card>
    );
  }

  const maxValue = rows.reduce((m, r) => Math.max(m, r.planned, r.actual), 0);

  return (
    <Card className="h-full">
      <CardContent>
        <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
          Bucket Breakdown
        </Typography>
        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2.5 }}>
          {rows.map((row) => {
            const baseWidth = maxValue > 0 ? (Math.min(row.planned, row.actual) / maxValue) * 100 : 0;
            const overage = Math.max(0, row.actual - row.planned);
            const overageWidth = maxValue > 0 ? (overage / maxValue) * 100 : 0;
            const underUsed = row.actual < row.planned;
            return (
              <Box key={row.bucket}>
                <Box className="flex items-baseline justify-between">
                  <Typography variant="body2" sx={{ fontWeight: 700, color: semantic.ink }}>
                    {row.bucket}
                  </Typography>
                  <Typography variant="caption" sx={{ color: semantic.expense }}>
                    {`${currency.format(row.actual)} of ${currency.format(row.planned)}`}
                    {overage > 0 && ` (+${currency.format(overage)})`}
                  </Typography>
                </Box>
                <Box
                  sx={{
                    mt: 0.75,
                    position: 'relative',
                    height: 14,
                    borderRadius: 7,
                    backgroundColor: alpha(semantic.neutral, 0.25),
                    overflow: 'hidden',
                    display: 'flex',
                  }}
                >
                  <Box
                    sx={{
                      width: `${baseWidth}%`,
                      height: '100%',
                      backgroundColor: underUsed
                        ? alpha(semantic.income, 0.65)
                        : semantic.neutral,
                    }}
                  />
                  {overage > 0 && (
                    <Box
                      sx={{
                        width: `${overageWidth}%`,
                        height: '100%',
                        backgroundColor: semantic.overspend,
                      }}
                    />
                  )}
                </Box>
              </Box>
            );
          })}
        </Box>
      </CardContent>
    </Card>
  );
}
