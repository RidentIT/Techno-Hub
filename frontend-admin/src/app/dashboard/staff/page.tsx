"use client";

import { useCallback, useEffect, useState } from "react";
import { Loader2 } from "lucide-react";

import { CreateStaffForm } from "@/components/staff/create-staff-form";
import { StaffTable } from "@/components/staff/staff-table";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { ApiError } from "@/lib/api-client";
import { useAuth } from "@/lib/auth-context";
import { staffApi } from "@/lib/staff-api";
import type { ScopeGroup, StaffUser } from "@/lib/types";

export default function StaffAdminPage() {
  const { isAdmin, user: currentUser, reloadUser } = useAuth();

  const [scopeGroups, setScopeGroups] = useState<ScopeGroup[]>([]);
  const [users, setUsers] = useState<StaffUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isAdmin) {
      setLoading(false);
      return;
    }

    let cancelled = false;

    void (async () => {
      try {
        const [groups, staff] = await Promise.all([staffApi.scopeGroups(), staffApi.listUsers()]);

        if (cancelled) return;
        setScopeGroups(groups);
        setUsers(staff);
      } catch (caught) {
        if (cancelled) return;
        setError(
          caught instanceof ApiError
            ? caught.message
            : "Could not load staff data. Is the API running?",
        );
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isAdmin]);

  const handleCreated = useCallback((created: StaffUser) => {
    setUsers((existing) => [created, ...existing]);
  }, []);

  const handleUpdated = useCallback(
    (updated: StaffUser) => {
      setUsers((existing) =>
        existing.map((user) => (user.id === updated.id ? updated : user)),
      );

      // If an Admin edited their own row, refresh the session copy so the sidebar and
      // hasScope() calls reflect it immediately.
      if (updated.id === currentUser?.id) {
        void reloadUser();
      }
    },
    [currentUser?.id, reloadUser],
  );

  if (!isAdmin) {
    return (
      <Alert variant="destructive">
        <AlertTitle>Administrators only</AlertTitle>
        <AlertDescription>
          Creating accounts and changing permissions requires the Admin role. No combination of
          scopes grants access to this page.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Staff &amp; permissions</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Create staff accounts and control what each one can reach. Permissions are scopes, assigned
          per account and independent of the role.
        </p>
      </div>

      {error ? (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}

      {loading ? (
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading staff data…
        </div>
      ) : (
        <>
          <CreateStaffForm scopeGroups={scopeGroups} onCreated={handleCreated} />
          <StaffTable users={users} scopeGroups={scopeGroups} onUpdated={handleUpdated} />
        </>
      )}
    </div>
  );
}
