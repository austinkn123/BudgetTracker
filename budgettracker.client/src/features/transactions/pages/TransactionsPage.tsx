import { useState } from 'react';
import { useTransactions } from '../hooks/useTransactions';
import StatusBanner from '../../../shared/components/StatusBanner';
import TransactionsSection from '../TransactionsSection';

const TransactionsPage = () => {
  const { isLoading } = useTransactions();
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-ink">Transactions</h1>
        <p className="mt-1 text-sm text-ink-muted">
          Browse your ledger by calendar day, then drill into that date to add or edit transactions
        </p>
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
