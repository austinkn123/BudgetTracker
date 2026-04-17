import Typography from '@mui/material/Typography';

export function DashboardHeader() {

  return (
    <header className="bg-white border-b border-gray-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <Typography variant="h4" className="font-bold text-gray-900">
          Budget Tracker
        </Typography>
      </div>
    </header>
  );
}
