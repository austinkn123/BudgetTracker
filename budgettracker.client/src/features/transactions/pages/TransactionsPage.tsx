import { useState } from 'react';
import { PageHeader } from '../../../shared/components/ui';
import { useTransactionSummary } from '../hooks/useTransactionSummary';
import StatusBanner from '../../../shared/components/StatusBanner';
import TransactionsSection from '../TransactionsSection';

const TransactionsPage = () => {
  // The section's first paint waits on the ledger summary: small, shared with the section's own
  // backlog count, and the one read every view needs. Each view's rows then load on their own.
  const { isLoading } = useTransactionSummary();
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);

  return (
    <div className="stagger-in space-y-6">
      <PageHeader
        title="Transactions"
        description="Review what your bank imported and give each transaction a category."
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
