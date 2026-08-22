import { NextResponse, type NextRequest } from "next/server";

import { isAuthResponse, postToBackend, problem, sessionResponse } from "@/lib/server/backend";

/**
 * POST /api/auth/login
 *
 * Forwards credentials to the staff API, then keeps the refresh token server-side in an httpOnly
 * cookie and returns only the access token and profile to the browser.
 */
export async function POST(request: NextRequest) {
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return problem(400, "Bad request", "The request body must be JSON.", "invalid_body");
  }

  const { status, payload } = await postToBackend("/api/staff/auth/login", body);

  if (status !== 200 || !isAuthResponse(payload)) {
    // Pass the API's own ProblemDetails through so the form can show the real message.
    return NextResponse.json(
      payload ?? {
        status,
        title: "Login failed",
        detail: "The staff API did not return a session.",
        errorCode: "login_failed",
      },
      { status: status === 200 ? 502 : status },
    );
  }

  return sessionResponse(payload);
}
