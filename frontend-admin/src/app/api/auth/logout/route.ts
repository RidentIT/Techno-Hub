import { NextResponse, type NextRequest } from "next/server";

import { SESSION_COOKIE_NAME, clearSessionCookie, postToBackend } from "@/lib/server/backend";

/**
 * POST /api/auth/logout
 *
 * Revokes the refresh token server-side, then clears the cookie. Always succeeds from the client's
 * point of view: if the API is unreachable we still drop the cookie locally, so the user is signed
 * out of this browser either way.
 */
export async function POST(request: NextRequest) {
  const refreshToken = request.cookies.get(SESSION_COOKIE_NAME)?.value;

  if (refreshToken) {
    try {
      await postToBackend("/api/staff/auth/logout", { refreshToken });
    } catch {
      // Swallowed deliberately — see above.
    }
  }

  return clearSessionCookie(new NextResponse(null, { status: 204 }));
}
