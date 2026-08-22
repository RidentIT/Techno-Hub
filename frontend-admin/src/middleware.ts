import { NextResponse, type NextRequest } from "next/server";

const SESSION_COOKIE_NAME = process.env.SESSION_COOKIE_NAME ?? "th_admin_session";

/**
 * Coarse route guard.
 *
 * Middleware can only see the session cookie, not validate it — the signing key lives in the API,
 * and calling the API from middleware on every navigation would be slow and still racy. So this is
 * a cheap redirect for the obvious cases:
 *
 *  - no cookie at all and heading somewhere protected → straight to /login, no flash of the shell
 *  - cookie present and heading to /login → straight to /dashboard
 *
 * The real gate is {@link ../components/auth-guard AuthGuard} in the dashboard layout, which
 * exchanges the cookie for an access token and finds out whether it is actually still valid. A
 * revoked or expired cookie gets past this middleware and is rejected there.
 */
export function middleware(request: NextRequest) {
  const hasSession = Boolean(request.cookies.get(SESSION_COOKIE_NAME)?.value);
  const { pathname, search } = request.nextUrl;

  const isLoginRoute = pathname === "/login";

  if (!hasSession && !isLoginRoute) {
    const loginUrl = new URL("/login", request.url);

    // Remember where they were headed so login can bounce them back.
    if (pathname !== "/") {
      loginUrl.searchParams.set("next", `${pathname}${search}`);
    }

    return NextResponse.redirect(loginUrl);
  }

  if (hasSession && isLoginRoute) {
    return NextResponse.redirect(new URL("/dashboard", request.url));
  }

  return NextResponse.next();
}

export const config = {
  /*
    Everything except Next internals, static assets and the auth route handlers. The handlers must
    stay reachable without a cookie — /api/auth/login is how you get one in the first place.
  */
  matcher: ["/((?!_next/static|_next/image|favicon.ico|api/auth).*)"],
};
