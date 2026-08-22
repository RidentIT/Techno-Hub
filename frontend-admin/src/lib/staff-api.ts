import { apiFetch } from "@/lib/api-client";
import type { RegisterStaffPayload, ScopeGroup, StaffUser } from "@/lib/types";

/** Typed wrappers over the staff endpoints, so components never build URLs by hand. */
export const staffApi = {
  /** The scope catalogue, grouped by module, in the backend's display order. */
  scopeGroups: () => apiFetch<ScopeGroup[]>("/api/staff/scopes"),

  /** Every staff account. Needs `staff.view`, or the Admin role. */
  listUsers: () => apiFetch<StaffUser[]>("/api/staff/users"),

  /** Creates a Technician or User. Admin-only. */
  register: (payload: RegisterStaffPayload) =>
    apiFetch<StaffUser>("/api/staff/auth/register", {
      method: "POST",
      body: JSON.stringify(payload),
    }),

  /** Replaces a user's scope set. The list is absolute — `[]` removes everything. */
  updateScopes: (userId: string, scopes: string[]) =>
    apiFetch<StaffUser>(`/api/staff/users/${userId}/scopes`, {
      method: "PATCH",
      body: JSON.stringify({ scopes }),
    }),

  /** Activates or soft-disables an account. Staff are never hard-deleted. */
  updateStatus: (userId: string, isActive: boolean, reason?: string) =>
    apiFetch<StaffUser>(`/api/staff/users/${userId}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive, reason: reason ?? null }),
    }),
};
