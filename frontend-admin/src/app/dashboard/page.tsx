"use client";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { useAuth } from "@/lib/auth-context";
import { formatDateTime } from "@/lib/utils";

export default function DashboardPage() {
  const { user, role, scopes, isAdmin } = useAuth();

  if (!user) return null;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Welcome back, {user.fullName}</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Module 1 of the Techno Hub system: staff authentication and permissions. Inventory, sales,
          repairs and quotations arrive in later modules.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Card>
          <CardHeader className="pb-3">
            <CardDescription>Signed in as</CardDescription>
            <CardTitle className="text-base">{user.email}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm text-muted-foreground">
            <p>Username: {user.userName}</p>
            <p>Phone: {user.phoneNumber ?? "—"}</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardDescription>Role</CardDescription>
            <CardTitle className="text-base">
              <Badge variant={isAdmin ? "default" : "secondary"}>{role}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="text-sm text-muted-foreground">
            {isAdmin
              ? "Full access. Passes every permission check regardless of assigned scopes."
              : "Access is limited to the scopes assigned to your account."}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardDescription>Account</CardDescription>
            <CardTitle className="text-base">
              <Badge variant={user.isActive ? "success" : "destructive"}>
                {user.isActive ? "Active" : "Deactivated"}
              </Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm text-muted-foreground">
            <p>Created: {formatDateTime(user.createdAt)}</p>
            <p>Last login: {formatDateTime(user.lastLoginAt)}</p>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Your permissions</CardTitle>
          <CardDescription>
            {isAdmin
              ? "As an Admin you hold no individual scopes — the role itself satisfies every check."
              : `${scopes.length} scope${scopes.length === 1 ? "" : "s"} assigned to this account.`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isAdmin ? (
            <Badge>All permissions</Badge>
          ) : scopes.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No scopes assigned yet. An administrator needs to grant permissions before you can use
              any module.
            </p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {scopes.map((scope) => (
                <Badge key={scope} variant="outline" className="font-mono text-xs">
                  {scope}
                </Badge>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
