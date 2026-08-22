"use client";

import { useState } from "react";
import { Loader2, UserPlus } from "lucide-react";

import { ScopePicker } from "@/components/staff/scope-picker";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input, Select } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api-client";
import { SCOPES } from "@/lib/scopes";
import { staffApi } from "@/lib/staff-api";
import { ASSIGNABLE_ROLES, type ScopeGroup, type StaffRole, type StaffUser } from "@/lib/types";

/**
 * Scopes a new account starts with, mirroring the backend's ResolveInitialScopes. Switching the role
 * resets the selection to this, which keeps the checkboxes honest about what the role means — the
 * Admin can then adjust freely, because the Technician defaults are ordinary revocable grants.
 */
const ROLE_DEFAULT_SCOPES: Record<StaffRole, string[]> = {
  Admin: [],
  Technician: [SCOPES.repairsView, SCOPES.repairsManage],
  User: [],
};

interface CreateStaffFormProps {
  scopeGroups: ScopeGroup[];
  onCreated: (user: StaffUser) => void;
}

export function CreateStaffForm({ scopeGroups, onCreated }: CreateStaffFormProps) {
  const [role, setRole] = useState<StaffRole>("Technician");
  const [email, setEmail] = useState("");
  const [userName, setUserName] = useState("");
  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [password, setPassword] = useState("");
  const [scopes, setScopes] = useState<string[]>(ROLE_DEFAULT_SCOPES.Technician);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [success, setSuccess] = useState<string | null>(null);

  function handleRoleChange(next: StaffRole) {
    setRole(next);
    setScopes(ROLE_DEFAULT_SCOPES[next]);
  }

  function resetForm() {
    setEmail("");
    setUserName("");
    setFullName("");
    setPhoneNumber("");
    setPassword("");
    setScopes(ROLE_DEFAULT_SCOPES[role]);
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setFieldErrors({});
    setSuccess(null);
    setSubmitting(true);

    try {
      const created = await staffApi.register({
        email: email.trim(),
        userName: userName.trim() === "" ? null : userName.trim(),
        fullName: fullName.trim(),
        phoneNumber: phoneNumber.trim() === "" ? null : phoneNumber.trim(),
        password,
        role,
        // Always explicit, so what the checkboxes show is exactly what gets granted.
        scopes,
      });

      onCreated(created);
      setSuccess(`Created ${created.fullName} (${created.email}) as ${created.role}.`);
      resetForm();
    } catch (caught) {
      if (caught instanceof ApiError) {
        setFieldErrors(caught.fieldErrors);
        setError(caught.message);
      } else {
        setError("Could not reach the staff API.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  const fieldError = (field: string) => fieldErrors[field]?.[0];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Create a staff account</CardTitle>
        <CardDescription>
          Admin-only. There is no self-registration, so this is the only way an account is created.
          Admin accounts cannot be created here.
        </CardDescription>
      </CardHeader>

      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-5" noValidate>
          {error ? (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : null}

          {success ? (
            <Alert variant="success">
              <AlertDescription>{success}</AlertDescription>
            </Alert>
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="new-fullName">Full name</Label>
              <Input
                id="new-fullName"
                required
                value={fullName}
                onChange={(event) => setFullName(event.target.value)}
                aria-invalid={Boolean(fieldError("fullName"))}
                disabled={submitting}
              />
              <FieldError message={fieldError("fullName")} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="new-role">Role</Label>
              <Select
                id="new-role"
                value={role}
                onChange={(event) => handleRoleChange(event.target.value as StaffRole)}
                disabled={submitting}
              >
                {ASSIGNABLE_ROLES.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </Select>
              <p className="text-xs text-muted-foreground">
                {role === "Technician"
                  ? "Starts with the repairs permissions, which you can change below."
                  : "Starts with no permissions at all."}
              </p>
              <FieldError message={fieldError("role")} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="new-email">Email</Label>
              <Input
                id="new-email"
                type="email"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                aria-invalid={Boolean(fieldError("email"))}
                disabled={submitting}
              />
              <FieldError message={fieldError("email")} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="new-userName">
                Username <span className="text-muted-foreground">(optional)</span>
              </Label>
              <Input
                id="new-userName"
                value={userName}
                onChange={(event) => setUserName(event.target.value)}
                placeholder="Defaults to the email"
                aria-invalid={Boolean(fieldError("userName"))}
                disabled={submitting}
              />
              <FieldError message={fieldError("userName")} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="new-phone">
                Phone <span className="text-muted-foreground">(optional)</span>
              </Label>
              <Input
                id="new-phone"
                value={phoneNumber}
                onChange={(event) => setPhoneNumber(event.target.value)}
                aria-invalid={Boolean(fieldError("phoneNumber"))}
                disabled={submitting}
              />
              <FieldError message={fieldError("phoneNumber")} />
            </div>

            <div className="space-y-2">
              <Label htmlFor="new-password">Initial password</Label>
              <Input
                id="new-password"
                type="password"
                required
                autoComplete="new-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                aria-invalid={Boolean(fieldError("password"))}
                disabled={submitting}
              />
              <p className="text-xs text-muted-foreground">
                At least 10 characters with upper and lower case, a digit and a symbol.
              </p>
              <FieldError message={fieldError("password")} />
            </div>
          </div>

          <div className="space-y-3 border-t border-border pt-4">
            <div>
              <h3 className="text-sm font-semibold">Permissions</h3>
              <p className="text-xs text-muted-foreground">
                Grouped by module. These are independent of the role — a Technician and a User with
                the same boxes ticked can do exactly the same things.
              </p>
            </div>

            <ScopePicker
              groups={scopeGroups}
              selected={scopes}
              onChange={setScopes}
              disabled={submitting}
              idPrefix="new"
            />
            <FieldError message={fieldError("scopes")} />
          </div>

          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Creating…
              </>
            ) : (
              <>
                <UserPlus className="h-4 w-4" />
                Create account
              </>
            )}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="text-xs font-medium text-destructive">{message}</p>;
}
