import "server-only";

import { NextResponse } from "next/server";

import type { StaffUser } from "@/lib/types";

/**
 * Server-side plumbing for the three auth route handlers.
 *
 * These handlers are the reason the browser never touches a refresh token. The backend hands the
 * refresh token to this server, which stores it in an httpOnly cookie on the Next.js origin; only
 * the short-lived access token is passed on to the client, which keeps it in memory. Nothing
 * auth-related is ever written to localStorage or sessionStorage.
 */

export const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5080";

export const SESSION_COOKIE_NAME = process.env.SESSION_COOKIE_NAME ?? "th_admin_session";

/** The full login/refresh payload from the API, refresh token included. Never leaves the server. */
interface BackendAuthResponse {
  accessToken: string;
  tokenType: string;
  expiresInSeconds: number;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: StaffUser;
}

/** Calls a staff auth endpoint on the ASP.NET Core API. */
export async function postToBackend(
  path: string,
  body: unknown,
): Promise<{ status: number; payload: unknown }> {
  const response = await fetch(`${BACKEND_API_URL}${path}`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body ?? {}),
    // Auth calls must never be served from a cache.
    cache: "no-store",
  });

  // 204 (logout) has no body, and a crashed API may return HTML.
  const text = await response.text();
  let payload: unknown = null;
  if (text.length > 0) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = { title: "Upstream error", detail: text.slice(0, 500) };
    }
  }

  return { status: response.status, payload };
}

/**
 * Splits a backend auth payload into the part the browser may see and the refresh token, which is
 * written straight into the httpOnly cookie.
 */
export function sessionResponse(payload: BackendAuthResponse): NextResponse {
  const { refreshToken, refreshTokenExpiresAt, tokenType: _tokenType, ...session } = payload;

  const response = NextResponse.json(session);

  response.cookies.set({
    name: SESSION_COOKIE_NAME,
    value: refreshToken,
    httpOnly: true,
    // Plain http on localhost during development, so Secure would stop the cookie being stored.
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    expires: new Date(refreshTokenExpiresAt),
  });

  return response;
}

/** Removes the session cookie. Used on logout and whenever a refresh is rejected. */
export function clearSessionCookie(response: NextResponse): NextResponse {
  response.cookies.set({
    name: SESSION_COOKIE_NAME,
    value: "",
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: 0,
  });

  return response;
}

/** True when the payload looks like a successful auth response. */
export function isAuthResponse(payload: unknown): payload is BackendAuthResponse {
  return (
    typeof payload === "object" &&
    payload !== null &&
    typeof (payload as BackendAuthResponse).accessToken === "string" &&
    typeof (payload as BackendAuthResponse).refreshToken === "string"
  );
}

/** A ProblemDetails-shaped body, for failures that never reach the API. */
export function problem(status: number, title: string, detail: string, errorCode: string) {
  return NextResponse.json({ status, title, detail, errorCode }, { status });
}
