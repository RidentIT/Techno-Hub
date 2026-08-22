import { useAuthStore } from "@/lib/auth-store";
import type { ApiProblem, SessionResponse } from "@/lib/types";

/**
 * Data calls go straight from the browser to the ASP.NET Core API with the in-memory access token
 * as a Bearer header. Only login/refresh/logout go through this app's own route handlers, because
 * only those need to touch the httpOnly refresh cookie.
 */
const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

/** A failed API call, carrying the ProblemDetails body the backend returned. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ApiProblem,
  ) {
    super(problem.detail ?? problem.title ?? `Request failed with status ${status}`);
    this.name = "ApiError";
  }

  /** Per-field validation messages, keyed by camelCase field name. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {};
  }

  /** First message for a field, if any. */
  fieldError(field: string): string | undefined {
    return this.fieldErrors[field]?.[0];
  }
}

let refreshInFlight: Promise<SessionResponse | null> | null = null;

/**
 * Refreshes the session, collapsing concurrent callers onto a single request.
 *
 * The de-duplication matters more than it looks: the backend rotates refresh tokens and treats a
 * replayed one as a stolen credential, revoking every session for the account. Two parallel 401s
 * each firing their own refresh would present the same cookie twice and log the user out for good.
 */
export function refreshSession(): Promise<SessionResponse | null> {
  if (!refreshInFlight) {
    refreshInFlight = performRefresh().finally(() => {
      refreshInFlight = null;
    });
  }

  return refreshInFlight;
}

async function performRefresh(): Promise<SessionResponse | null> {
  try {
    const response = await fetch("/api/auth/refresh", {
      method: "POST",
      cache: "no-store",
    });

    if (!response.ok) {
      return null;
    }

    const session = (await response.json()) as SessionResponse;
    useAuthStore.getState().setSession(session);
    return session;
  } catch {
    // Network failure — treat as "no session" and let the caller redirect to login.
    return null;
  }
}

/**
 * Calls the staff API with the current access token, refreshing once on a 401 before giving up.
 */
export async function apiFetch<T>(
  path: string,
  init: RequestInit = {},
  { allowRetry = true }: { allowRetry?: boolean } = {},
): Promise<T> {
  const { accessToken } = useAuthStore.getState();

  const headers = new Headers(init.headers);
  if (init.body !== undefined && !headers.has("content-type")) {
    headers.set("content-type", "application/json");
  }
  if (accessToken) {
    headers.set("authorization", `Bearer ${accessToken}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
    cache: "no-store",
  });

  if (response.status === 401 && allowRetry) {
    const session = await refreshSession();

    if (session) {
      return apiFetch<T>(path, init, { allowRetry: false });
    }

    useAuthStore.getState().clear();
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function readProblem(response: Response): Promise<ApiProblem> {
  try {
    const body = (await response.json()) as ApiProblem;
    if (body && typeof body === "object") {
      return body;
    }
  } catch {
    // Fall through to the generic shape below.
  }

  return {
    status: response.status,
    title: response.statusText || "Request failed",
    detail: `The API returned ${response.status}.`,
  };
}
