import { defineConfig } from "orval";

export default defineConfig({
  hrs: {
    input: {
      target: "http://localhost:8080/openapi/v1.json",
    },
    output: {
      mode: "tags-split",
      target: "src/lib/api/generated",
      client: "react-query",
      httpClient: "fetch",
      clean: true,
      override: {
        mutator: {
          path: "src/lib/api/client.ts",
          name: "customFetch",
        },
        fetch: {
          // customFetch() already resolves with the parsed body alone (throwing ApiError on
          // a non-2xx response) — without this, orval assumes a {data, status, headers}
          // envelope that customFetch never actually produces.
          includeHttpResponseReturnType: false,
        },
      },
    },
  },
});
