import Typography from '@mui/material/Typography';

export function DashboardHeader() {

  return (
    <header className="bg-surface border-b border-border">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <Typography variant="h4" className="font-bold text-ink">
          Budget Tracker
        </Typography>
      </div>
    </header>
  );
}
