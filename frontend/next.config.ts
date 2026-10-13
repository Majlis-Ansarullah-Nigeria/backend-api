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
    if (process.env.NODE_ENV !== "development") {
      return [];
    }

    return [
      {
        source: "/api/:path*",
        destination: "http://localhost:8080/api/:path*",
      },
      {
        source: "/hubs/:path*",
        destination: "http://localhost:8080/hubs/:path*",
      },
    ];
  },
};

export default withNextIntl(nextConfig);
