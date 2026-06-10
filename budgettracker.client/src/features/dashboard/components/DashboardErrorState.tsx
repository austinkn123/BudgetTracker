type DashboardErrorStateProps = {
  userError: unknown;
  categoriesError: unknown;
  transactionsError: unknown;
  budgetPlansError: unknown;
};

export function DashboardErrorState({
  userError,
  categoriesError,
  transactionsError,
  budgetPlansError,
}: DashboardErrorStateProps) {
  return (
    <div className="space-y-8">
      <div className="bg-error-subtle border border-error/30 rounded-lg p-4">
        <p className="text-error-dark text-sm">
          <strong>Error:</strong> Unable to connect to the database. Please ensure the database is running and properly configured.
        </p>
        {Boolean(userError) && <p className="text-error-dark text-xs mt-2">User service: {String(userError)}</p>}
        {Boolean(categoriesError) && <p className="text-error-dark text-xs mt-2">Category service: {String(categoriesError)}</p>}
        {Boolean(transactionsError) && <p className="text-error-dark text-xs mt-2">Transaction service: {String(transactionsError)}</p>}
        {Boolean(budgetPlansError) && <p className="text-error-dark text-xs mt-2">Budget plan service: {String(budgetPlansError)}</p>}
      </div>
    </div>
  );
}
