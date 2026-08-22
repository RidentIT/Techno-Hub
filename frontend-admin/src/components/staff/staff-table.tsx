"use client";

import { useState } from "react";
import { Loader2, Pencil, Power, X } from "lucide-react";

import { ScopePicker } from "@/components/staff/scope-picker";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ApiError } from "@/lib/api-client";
import { useAuth } from "@/lib/auth-context";
import { staffApi } from "@/lib/staff-api";
import type { ScopeGroup, StaffUser } from "@/lib/types";
import { formatDateTime } from "@/lib/utils";

interface StaffTableProps {
  users: StaffUser[];
  scopeGroups: ScopeGroup[];
  onUpdated: (user: StaffUser) => void;
}

export function StaffTable({ users, scopeGroups, onUpdated }: StaffTableProps) {
  const { user: currentUser } = useAuth();

  const [editingId, setEditingId] = useState<string | null>(null);
  const [draftScopes, setDraftScopes] = useState<string[]>([]);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  function startEditing(user: StaffUser) {
    setError(null);
    setEditingId(user.id);
    setDraftScopes(user.scopes);
  }

  function cancelEditing() {
    setEditingId(null);
    setDraftScopes([]);
  }

  async function saveScopes(user: StaffUser) {
    setBusyId(user.id);
    setError(null);

    try {
      onUpdated(await staffApi.updateScopes(user.id, draftScopes));
      cancelEditing();
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Could not update permissions.");
    } finally {
      setBusyId(null);
    }
  }

  async function toggleStatus(user: StaffUser) {
    setBusyId(user.id);
    setError(null);

    try {
      onUpdated(
        await staffApi.updateStatus(
          user.id,
          !user.isActive,
          user.isActive ? "Deactivated from the staff console" : "Reactivated from the staff console",
        ),
      );
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : "Could not change the account status.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Staff accounts</CardTitle>
        <CardDescription>
          {users.length} account{users.length === 1 ? "" : "s"}. Accounts are deactivated rather than
          deleted, so history that references them stays intact.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        {error ? (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : null}

        {users.length === 0 ? (
          <p className="text-sm text-muted-foreground">No staff accounts yet.</p>
        ) : (
          <div className="space-y-3">
            {users.map((user) => {
              const isSelf = user.id === currentUser?.id;
              const isAdminRow = user.role === "Admin";
              const busy = busyId === user.id;
              const editing = editingId === user.id;

              return (
                <div key={user.id} className="rounded-md border border-border p-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-medium">{user.fullName}</span>
                        <Badge variant={isAdminRow ? "default" : "secondary"}>{user.role}</Badge>
                        <Badge variant={user.isActive ? "success" : "destructive"}>
                          {user.isActive ? "Active" : "Deactivated"}
                        </Badge>
                        {isSelf ? <Badge variant="outline">You</Badge> : null}
                      </div>

                      <p className="mt-1 truncate text-sm text-muted-foreground">
                        {user.email}
                        {user.userName !== user.email ? ` · ${user.userName}` : ""}
                      </p>
                      <p className="mt-0.5 text-xs text-muted-foreground">
                        Created {formatDateTime(user.createdAt)} · Last login{" "}
                        {formatDateTime(user.lastLoginAt)}
                      </p>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      {!isAdminRow ? (
                        editing ? (
                          <>
                            <Button
                              size="sm"
                              onClick={() => void saveScopes(user)}
                              disabled={busy}
                            >
                              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                              Save permissions
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={cancelEditing}
                              disabled={busy}
                            >
                              <X className="h-4 w-4" />
                              Cancel
                            </Button>
                          </>
                        ) : (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => startEditing(user)}
                            disabled={busy}
                          >
                            <Pencil className="h-4 w-4" />
                            Edit permissions
                          </Button>
                        )
                      ) : null}

                      <Button
                        size="sm"
                        variant={user.isActive ? "destructive" : "outline"}
                        onClick={() => void toggleStatus(user)}
                        /*
                          The API refuses to deactivate your own account or the last active Admin;
                          disabling the button for the self case just avoids a pointless round trip.
                        */
                        disabled={busy || (isSelf && user.isActive)}
                        title={
                          isSelf && user.isActive ? "You cannot deactivate your own account" : undefined
                        }
                      >
                        {busy ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Power className="h-4 w-4" />
                        )}
                        {user.isActive ? "Deactivate" : "Activate"}
                      </Button>
                    </div>
                  </div>

                  <div className="mt-3 border-t border-border pt-3">
                    {editing ? (
                      <ScopePicker
                        groups={scopeGroups}
                        selected={draftScopes}
                        onChange={setDraftScopes}
                        disabled={busy}
                        idPrefix={`edit-${user.id}`}
                      />
                    ) : isAdminRow ? (
                      <p className="text-sm text-muted-foreground">
                        Admins bypass every permission check, so they hold no individual scopes.
                      </p>
                    ) : user.scopes.length === 0 ? (
                      <p className="text-sm text-muted-foreground">
                        No permissions assigned — this account cannot access any module yet.
                      </p>
                    ) : (
                      <div className="flex flex-wrap gap-1.5">
                        {user.scopes.map((scope) => (
                          <Badge key={scope} variant="outline" className="font-mono text-[10px]">
                            {scope}
                          </Badge>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
