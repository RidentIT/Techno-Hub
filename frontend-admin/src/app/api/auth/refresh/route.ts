import { NextResponse, type NextRequest } from "next/server";

import {
  SESSION_COOKIE_NAME,
  clearSessionCookie,
  isAuthResponse,
  postToBackend,
  sessionResponse,
} from "@/lib/server/backend";

/**
 * POST /api/auth/refresh
 *
 * Exchanges the httpOnly cookie for a new access token. Called on first paint to rebuild the
 * in-memory session after a page reload, and again whenever an API call comes back 401.
 *
 * Refresh tokens rotate on every use, so the cookie is rewritten with the replacement here. If the
 * exchange fails the cookie is cleared, which is what stops the client retrying with a token the
 * backend has already revoked.
 */
export async function POST(request: NextRequest) {
  const refreshToken = request.cookies.get(SESSION_COOKIE_NAME)?.value;

  if (!refreshToken) {
    // Not an error worth logging — this is the normal state for a first-time visitor.
    return NextResponse.json(
      {
        status: 401,
        title: "Unauthorized",
        detail: "No session cookie present.",
        errorCode: "no_session",
      },
      { status: 401 },
    );
  }

  const { status, payload } = await postToBackend("/api/staff/auth/refresh", { refreshToken });

  if (status !== 200 || !isAuthResponse(payload)) {
    const response = NextResponse.json(
      payload ?? {
        status,
        title: "Session expired",
        detail: "The refresh token was rejected.",
        errorCode: "refresh_failed",
      },
      { status: status === 200 ? 502 : status },
    );

    return clearSessionCookie(response);
  }

  return sessionResponse(payload);
}
