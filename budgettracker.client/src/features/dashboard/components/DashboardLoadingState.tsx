import { Card, Skeleton } from '../../../shared/components/ui';

/**
 * Skeleton mirror of the dashboard layout (BUD-20). Matching the real
 * composition keeps the page from jumping when data lands.
 */
const DashboardLoadingState = () => (
  <div className="space-y-8">
    {/* Header */}
    <div className="flex flex-col gap-4 rounded-md border border-border bg-surface px-5 py-5 shadow-sm sm:flex-row sm:items-end sm:justify-between">
      <div className="space-y-2">
        <Skeleton width={140} height={28} />
        <Skeleton width={220} height={14} />
      </div>
      <Skeleton width={260} height={36} className="rounded-full" />
    </div>

    {/* Hero */}
    <Skeleton height={320} className="rounded-md" />

    {/* Waterfall + Where it went */}
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
      <div className="lg:col-span-2">
        <Card>
          <Skeleton width={180} height={18} />
          <Skeleton height={288} className="mt-4 rounded" />
        </Card>
      </div>
      <Card>
        <Skeleton width={140} height={18} />
        <div className="mt-4 space-y-3">
          {Array.from({ length: 6 }, (_, i) => (
            <div key={i} className="space-y-1.5">
              <Skeleton width={`${60 + ((i * 7) % 30)}%`} height={12} />
              <Skeleton height={10} className="rounded-full" />
            </div>
          ))}
        </div>
      </Card>
    </div>

    {/* Drill grid */}
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {Array.from({ length: 4 }, (_, i) => (
        <Card key={i}>
          <Skeleton width="70%" height={14} />
          <Skeleton width="50%" height={12} className="mt-2" />
          <Skeleton height={8} className="mt-3 rounded-full" />
          <Skeleton height={40} className="mt-3 rounded" />
        </Card>
      ))}
    </div>
  </div>
);

export default DashboardLoadingState;
