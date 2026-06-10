type DashboardStatusBannerProps = {
  statusMessage: string | null;
  statusError: string | null;
};

export function DashboardStatusBanner({ statusMessage, statusError }: DashboardStatusBannerProps) {
  if (!statusMessage && !statusError) {
    return null;
  }

  return (
    <section
      className={`rounded-lg border px-4 py-3 text-sm ${
        statusError ? 'bg-error-subtle border-error/30 text-error-dark' : 'bg-success-subtle border-success/30 text-success-dark'
      }`}
    >
      {statusError || statusMessage}
    </section>
  );
}
