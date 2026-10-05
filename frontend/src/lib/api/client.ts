export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly details?: unknown,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

function resolveBaseUrl(): string {
  // Browser: same-origin, Traefik (prod) / next.config.ts rewrite (dev) route /api/* to the API.
  if (typeof window !== "undefined") {
    return "";
  }
  // Server Components / route handlers talk to the API directly, bypassing Traefik.
  return process.env.API_INTERNAL_URL ?? "http://localhost:8080";
}

async function withForwardedCookie(headers: Headers): Promise<Headers> {
  if (typeof window !== "undefined") {
    return headers;
  }

  const { cookies } = await import("next/headers");
  const cookieStore = await cookies();
  const cookieHeader = cookieStore.toString();

  if (cookieHeader) {
    headers.set("cookie", cookieHeader);
  }

  return headers;
}

export async function customFetch<T>(url: string, options: RequestInit = {}): Promise<T> {
  const baseUrl = resolveBaseUrl();
  const headers = await withForwardedCookie(new Headers(options.headers));

  const response = await fetch(`${baseUrl}${url}`, {
    ...options,
    headers,
    credentials: "include",
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as
      | { code?: string; title?: string; detail?: string }
      | null;

    throw new ApiError(
      response.status,
      problem?.code ?? "UNKNOWN_ERROR",
      problem?.detail ?? problem?.title ?? response.statusText,
      problem,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}
