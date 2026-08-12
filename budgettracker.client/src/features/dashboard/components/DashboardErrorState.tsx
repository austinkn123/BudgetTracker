import Typography from '@mui/material/Typography';
import BudAlert from '../../../shared/components/ui/BudAlert';

type DashboardErrorStateProps = {
  userError: unknown;
  categoriesError: unknown;
  transactionsError: unknown;
  analysisError: unknown;
};

const DashboardErrorState = ({
  userError,
  categoriesError,
  transactionsError,
  analysisError,
}: DashboardErrorStateProps) => {
  const failures = [
    { label: 'User service', error: userError },
    { label: 'Category service', error: categoriesError },
    { label: 'Transaction service', error: transactionsError },
    { label: 'Budget analysis service', error: analysisError },
  ].filter((entry) => Boolean(entry.error));

  return (
    <div className="space-y-8">
      <BudAlert
        severity="error"
        title="Unable to load your dashboard"
        message="We couldn't reach the API. Check that the server and database are running."
      >
        {failures.map(({ label, error }) => (
          <Typography key={label} variant="caption" sx={{ display: 'block', mt: 1 }}>
            {label}: {String(error)}
          </Typography>
        ))}
      </BudAlert>
    </div>
  );
};

export default DashboardErrorState;
