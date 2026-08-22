import { create } from "zustand";

import type { SessionResponse, StaffUser } from "@/lib/types";

export type AuthStatus = "loading" | "authenticated" | "unauthenticated";

interface AuthState {
  /** Short-lived JWT. In memory only. */
  accessToken: string | null;
  /** Access-token expiry as epoch milliseconds, used to schedule the silent refresh. */
  expiresAt: number | null;
  user: StaffUser | null;
  status: AuthStatus;

  setSession: (session: SessionResponse) => void;
  setUser: (user: StaffUser) => void;
  setStatus: (status: AuthStatus) => void;
  clear: () => void;
}

/**
 * Auth state for the admin app.
 *
 * Deliberately a plain in-memory store with no `persist` middleware: the access token must not
 * survive a reload in localStorage or sessionStorage, where any script on the page could read it.
 * After a reload the session is rebuilt by calling /api/auth/refresh, which reads the httpOnly
 * cookie the browser cannot see.
 */
export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  expiresAt: null,
  user: null,
  status: "loading",

  setSession: (session) =>
    set({
      accessToken: session.accessToken,
      expiresAt: new Date(session.accessTokenExpiresAt).getTime(),
      user: session.user,
      status: "authenticated",
    }),

  setUser: (user) => set({ user }),

  setStatus: (status) => set({ status }),

  clear: () =>
    set({
      accessToken: null,
      expiresAt: null,
      user: null,
      status: "unauthenticated",
    }),
}));
