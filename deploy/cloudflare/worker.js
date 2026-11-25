// Cloudflare front door. Static assets (the built SPA) are served by Cloudflare without running this script;
// only the run_worker_first routes in wrangler.jsonc (/api/*, /health*) reach it, and they go to the Lambda.
export default {
  async fetch(request, env) {
    if (!env.API_ORIGIN) {
      return new Response('API_ORIGIN is not set. Run: npx wrangler secret put API_ORIGIN', { status: 500 });
    }
    const url = new URL(request.url);
    const origin = new URL(env.API_ORIGIN);
    url.protocol = origin.protocol;
    url.host = origin.host;
    // Same method, headers and body; fetch sets Host from the new URL, which the Function URL requires.
    return fetch(new Request(url, request));
  },
};
