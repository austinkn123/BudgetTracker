import { useState } from 'react';
import { Download } from 'lucide-react';
import { Button } from '../../../shared/components/ui';
import { transactionService, type TransactionQuery } from '../../../shared/services/transaction.service';
import { downloadBlob } from '../../../shared/utils/downloadBlob';
import { errorMessage } from '../../../shared/utils/errorMessage';

/** Downloads exactly the rows the list shows, every page of them, as CSV (BUD-25). */
const ExportCsvButton = ({ query }: { query: TransactionQuery }) => {
  const [isExporting, setIsExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const exportCsv = async () => {
    setIsExporting(true);
    setError(null);
    try {
      const { blob, fileName } = await transactionService.exportCsv(query);
      downloadBlob(blob, fileName);
    } catch (err) {
      setError(errorMessage(err, "Couldn't export. Please try again."));
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div className="flex items-center gap-3">
      {error && (
        <span role="alert" className="text-xs text-error-dark">
          {error}
        </span>
      )}
      <Button
        variant="secondary"
        size="sm"
        onClick={() => void exportCsv()}
        loading={isExporting}
        startIcon={<Download size={14} aria-hidden />}
      >
        Export CSV
      </Button>
    </div>
  );
};

export default ExportCsvButton;
