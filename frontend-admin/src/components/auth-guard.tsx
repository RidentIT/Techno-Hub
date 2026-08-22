"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";

import { useAuth } from "@/lib/auth-context";

/**
 * The real authentication gate.
 *
 * The middleware only checks that a session cookie exists; this waits for AuthProvider to actually
 * exchange it for an access token and redirects to /login if that fails — which is what catches a
 * cookie that has expired, been revoked by a logout elsewhere, or belongs to an account an Admin has
 * since deactivated.
 */
export function AuthGuard({ children }: { children: React.ReactNode }) {
  const { status, isAuthenticated } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (status === "unauthenticated") {
      const loginUrl = `/login?next=${encodeURIComponent(pathname)}`;
      router.replace(loginUrl);
    }
  }, [status, pathname, router]);

  if (status === "loading") {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Restoring your session…
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    // Redirect is already in flight; render nothing rather than flashing the shell.
    return null;
  }

  return <>{children}</>;
}

/**
 * Renders children only when the user holds every one of the given scopes; Admins always pass.
 * Purely a UI convenience — the backend enforces the same rules, so hiding a control is about
 * keeping the interface honest, not about security.
 */
export function RequireScope({
  scopes,
  children,
  fallback = null,
}: {
  scopes: string[];
  children: React.ReactNode;
  fallback?: React.ReactNode;
}) {
  const { hasAllScopes } = useAuth();

  return hasAllScopes(...scopes) ? <>{children}</> : <>{fallback}</>;
}
