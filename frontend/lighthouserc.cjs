// M12.1 — NFR-PRF-02: Largest Contentful Paint under 2.5 s on a mid-range Android phone over 4G.
// Lighthouse's default mobile run is exactly that profile: a Moto G Power-class device, 4x CPU
// slowdown and simulated slow 4G (150 ms RTT, 1.6 Mbps). Run through perf/lighthouse.sh, which
// signs a seeded head in and supplies the URLs and the session cookie.
const base = process.env.LHCI_BASE_URL || "http://localhost:3000";
const reportPath = process.env.LHCI_REPORT_PATH;

module.exports = {
  ci: {
    collect: {
      url: [`${base}/login`, `${base}/home`, `${base}/reports`, ...(reportPath ? [`${base}${reportPath}`] : [])],
      numberOfRuns: 3,
      settings: {
        // The session cookie for the signed-in screens; harmless on /login.
        extraHeaders: process.env.LHCI_COOKIE ? JSON.stringify({ Cookie: process.env.LHCI_COOKIE }) : undefined,
        onlyCategories: ["performance", "accessibility"],
        disableStorageReset: true,
      },
    },
    assert: {
      assertions: {
        "largest-contentful-paint": ["error", { maxNumericValue: 2500, aggregationMethod: "median-run" }],
      },
    },
    upload: {
      target: "filesystem",
      outputDir: "./lighthouse-report",
    },
  },
};
