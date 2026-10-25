import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";

const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts");

const nextConfig: NextConfig = {
  output: "standalone",
  // The production Docker build's context is frontend/ only (docker-compose.dokploy.yml) and
  // can't see ../shared, which expression-evaluator.test.ts imports — test files are never part
  // of the shipped app, so excluding them here is correct, not just a workaround. `pnpm typecheck`
  // still checks them via the full tsconfig.json, where ../shared really is reachable.
  typescript: {
    tsconfigPath: "./tsconfig.build.json",
  },
  async rewrites() {
    // In production Traefik routes /api and /hubs to the API. `pnpm dev` proxies them here; a
    // production build only does when API_PROXY_URL is set at build time — the Lighthouse run
    // (M12.1, lighthouserc.cjs) uses that to measure a real build without Traefik.
    const apiUrl = process.env.NODE_ENV === "development" ? "http://localhost:8080" : process.env.API_PROXY_URL;
    if (!apiUrl) {
      return [];
    }

    return [
      {
        source: "/api/:path*",
        destination: `${apiUrl}/api/:path*`,
      },
      {
        source: "/hubs/:path*",
        destination: `${apiUrl}/hubs/:path*`,
      },
    ];
  },
};

export default withNextIntl(nextConfig);
