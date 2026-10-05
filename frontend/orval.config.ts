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
        query: {
          useQuery: true,
          useSuspenseQuery: true,
          useMutation: true,
        },
      },
    },
  },
});
