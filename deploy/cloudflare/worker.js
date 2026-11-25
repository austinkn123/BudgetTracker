// Cloudflare front door. Static assets (the built SPA) are served by Cloudflare without running this script;
// only the run_worker_first routes in wrangler.jsonc (/api/*, /health*) reach it, and they go to the Lambda.
export default {
  async fetch(request, env) {
    if (!env.API_ORIGIN) {
      return new Response('API_ORIGIN is not set. Run: npx wrangler secret put API_ORIGIN', { status: 500 });
    }
    if (!env.ORIGIN_SECRET) {
      return new Response('ORIGIN_SECRET is not set. Run: npx wrangler secret put ORIGIN_SECRET', { status: 500 });
    }
    const url = new URL(request.url);
    const origin = new URL(env.API_ORIGIN);
    url.protocol = origin.protocol;
    url.host = origin.host;
    // Same method, headers and body; fetch sets Host from the new URL, which the Function URL requires.
    const forwarded = new Request(url, request);
    // The Lambda rejects requests without this (CloudflareOriginGuard.cs). set() overwrites any client-sent value.
    forwarded.headers.set('X-Origin-Auth', env.ORIGIN_SECRET);
    return fetch(forwarded);
  },
};
