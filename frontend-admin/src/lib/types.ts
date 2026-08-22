/**
 * Mirrors the DTOs in TechnoHub.Application. Keep these in sync with the backend records —
 * the API serialises with camelCase property names.
 */

/** The three fixed staff roles. There is no customer identity in this system. */
export type StaffRole = "Admin" | "Technician" | "User";

/** Roles an Admin may create through the register endpoint. */
export const ASSIGNABLE_ROLES: readonly StaffRole[] = ["Technician", "User"];

export interface StaffUser {
  id: string;
  email: string;
  userName: string;
  fullName: string;
  phoneNumber: string | null;
  role: StaffRole;
  /** Always "staff". Mirrors the `type` claim on the JWT. */
  identityType: string;
  isActive: boolean;
  /** Explicitly granted scope keys. Empty for an Admin — see `hasAllScopes`. */
  scopes: string[];
  /** True for Admin, which passes every scope check by role. */
  hasAllScopes: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

/** What the login/refresh route handlers return to the browser. */
export interface SessionResponse {
  accessToken: string;
  expiresInSeconds: number;
  accessTokenExpiresAt: string;
  user: StaffUser;
}

export interface ScopeDefinition {
  key: string;
  module: string;
  description: string;
}

export interface ScopeGroup {
  module: string;
  scopes: ScopeDefinition[];
}

export interface RegisterStaffPayload {
  email: string;
  userName?: string | null;
  fullName: string;
  phoneNumber?: string | null;
  password: string;
  role: StaffRole;
  /** Omit to accept the role default; an empty array means "no scopes". */
  scopes?: string[] | null;
}

/** ProblemDetails as returned by the API's exception middleware. */
export interface ApiProblem {
  title?: string;
  detail?: string;
  status?: number;
  errorCode?: string;
  traceId?: string;
  /** Present on validation failures, keyed by camelCase field name. */
  errors?: Record<string, string[]>;
}
