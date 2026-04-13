type StatusBannerProps = {
  statusMessage: string | null;
  statusError: string | null;
};

export function StatusBanner({ statusMessage, statusError }: StatusBannerProps) {
  if (!statusMessage && !statusError) {
    return null;
  }

  return (
    <section
      className={`rounded-lg border px-4 py-3 text-sm ${
        statusError ? 'bg-red-50 border-red-200 text-red-800' : 'bg-green-50 border-green-200 text-green-800'
      }`}
    >
      {statusError || statusMessage}
    </section>
  );
}
