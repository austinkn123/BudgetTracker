import { Skeleton } from '../ui';

/**
 * Stand-in for a page while its code downloads (BUD-28): a header and two card blocks, so the
 * layout doesn't jump when the page arrives.
 */
const PageSkeleton = () => (
  <div className="space-y-6" role="status" aria-label="Loading page">
    <div className="space-y-2">
      <Skeleton width="12rem" height="1.75rem" />
      <Skeleton width="20rem" className="max-w-full" />
    </div>
    <Skeleton variant="rectangular" height="10rem" className="w-full" />
    <Skeleton variant="rectangular" height="16rem" className="w-full" />
  </div>
);

export default PageSkeleton;
