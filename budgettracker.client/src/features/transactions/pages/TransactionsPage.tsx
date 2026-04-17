import { useState } from 'react';
import Typography from '@mui/material/Typography';
import { useTransactions } from '../hooks/useTransactions';
import { StatusBanner } from '../../../shared/components/StatusBanner';
import TransactionsSection from '../TransactionsSection';

const TransactionsPage = () => {
  const { isLoading } = useTransactions();
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);

  return (
    <div className="space-y-6">
      <div>
        <Typography variant="h4" className="font-bold text-gray-900">
          Transactions
        </Typography>
        <Typography variant="body2" className="text-gray-500 mt-1">
          Record and manage your income and expenses
        </Typography>
      </div>

      <StatusBanner statusMessage={statusMessage} statusError={statusError} />

      <TransactionsSection
        isLoading={isLoading}
        setStatusMessage={setStatusMessage}
        setStatusError={setStatusError}
      />
    </div>
  );
};

export default TransactionsPage;
