import { useState } from 'react';
import { PageHeader } from '../../../shared/components/ui';
import { useTransactions } from '../hooks/useTransactions';
import StatusBanner from '../../../shared/components/StatusBanner';
import TransactionsSection from '../TransactionsSection';

const TransactionsPage = () => {
  const { isLoading } = useTransactions();
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Transactions"
        description="Review what your bank imported and give each transaction a category"
      />

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
