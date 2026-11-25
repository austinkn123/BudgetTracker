// Theme bootstrap (BUD-17). Runs before first paint so a dark-mode user
// never sees a white flash. Mirrors src/shared/theme/theme.ts — keep the
// storage key and the resolution rule in step with it. A file rather than an
// inline script so the CSP in public/_headers can stay free of 'unsafe-inline'.
(function () {
  try {
    var stored = window.localStorage.getItem('bud-theme');
    var preference = stored === 'light' || stored === 'dark' ? stored : 'system';
    var dark =
      preference === 'dark' ||
      (preference === 'system' &&
        window.matchMedia('(prefers-color-scheme: dark)').matches);
    document.documentElement.classList.toggle('dark', dark);
  } catch (e) {
    // Blocked storage or no matchMedia — fall through to the light default.
  }
})();
