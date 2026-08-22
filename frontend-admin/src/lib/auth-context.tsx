"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef } from "react";

import { ApiError, apiFetch, refreshSession } from "@/lib/api-client";
import { type AuthStatus, useAuthStore } from "@/lib/auth-store";
import type { SessionResponse, StaffRole, StaffUser } from "@/lib/types";

/** Refresh this many milliseconds before the access token actually expires. */
const REFRESH_LEEWAY_MS = 60_000;

interface AuthContextValue {
  user: StaffUser | null;
  role: StaffRole | null;
  scopes: string[];
  status: AuthStatus;
  isAuthenticated: boolean;

  login: (emailOrUsername: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  /** Re-reads the profile from the API, e.g. after an Admin changes your own account. */
  reloadUser: () => Promise<void>;

  /**
   * True when the current user may perform the given scope. Admins always pass, because the
   * backend's authorization handler short-circuits on the Admin role — mirroring that here keeps
   * the UI and the API in agreement.
   */
  hasScope: (scope: string) => boolean;
  /** True when the user holds every one of the given scopes. */
  hasAllScopes: (...scopes: string[]) => boolean;
  /** True when the user holds at least one of the given scopes. */
  hasAnyScope: (...scopes: string[]) => boolean;
  isAdmin: boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const { user, status, accessToken, expiresAt, setSession, setUser, setStatus, clear } =
    useAuthStore();

  const bootstrapped = useRef(false);

  // Rebuild the session from the httpOnly cookie on first paint. The in-memory access token is
  // gone after any reload, so this is what keeps a refresh of the page from bouncing to /login.
  useEffect(() => {
    if (bootstrapped.current) return;
    bootstrapped.current = true;

    let cancelled = false;

    void (async () => {
      const session = await refreshSession();
      if (cancelled) return;

      if (!session) {
        setStatus("unauthenticated");
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [setStatus]);

  // Silent refresh shortly before expiry, so a user sitting on a page doesn't hit a 401 mid-action.
  useEffect(() => {
    if (!accessToken || !expiresAt) return;

    const delay = Math.max(expiresAt - Date.now() - REFRESH_LEEWAY_MS, 5_000);
    const timer = setTimeout(() => {
      void refreshSession().then((session) => {
        if (!session) clear();
      });
    }, delay);

    return () => clearTimeout(timer);
  }, [accessToken, expiresAt, clear]);

  const login = useCallback(
    async (emailOrUsername: string, password: string) => {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ emailOrUsername, password }),
        cache: "no-store",
      });

      if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        throw new ApiError(response.status, problem);
      }

      setSession((await response.json()) as SessionResponse);
    },
    [setSession],
  );

  const logout = useCallback(async () => {
    try {
      await fetch("/api/auth/logout", { method: "POST", cache: "no-store" });
    } finally {
      // Clear locally even if the revoke call failed, so the UI never gets stuck signed in.
      clear();
    }
  }, [clear]);

  const reloadUser = useCallback(async () => {
    const profile = await apiFetch<StaffUser>("/api/staff/auth/me");
    setUser(profile);
  }, [setUser]);

  const hasScope = useCallback(
    (scope: string) => {
      if (!user) return false;
      return user.hasAllScopes || user.scopes.includes(scope);
    },
    [user],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      role: user?.role ?? null,
      scopes: user?.scopes ?? [],
      status,
      isAuthenticated: status === "authenticated" && user !== null,
      login,
      logout,
      reloadUser,
      hasScope,
      hasAllScopes: (...scopes: string[]) => scopes.every(hasScope),
      hasAnyScope: (...scopes: string[]) => scopes.some(hasScope),
      isAdmin: user?.role === "Admin",
    }),
    [user, status, login, logout, reloadUser, hasScope],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/** Current user, role, scopes and the auth actions. Must be used inside {@link AuthProvider}. */
export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error("useAuth must be used within an <AuthProvider>.");
  }

  return context;
}
