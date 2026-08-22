# Techno-Hub

Business management system for **Techno Hub**, a computer and tech hardware retailer.

**Module 1 — project scaffolding + staff authentication/authorization.**

---

## Contents

- [Repository layout](#repository-layout)
- [The authorization model](#the-authorization-model)
- [Prerequisites](#prerequisites)
- [Running locally](#running-locally)
  - [1. Backend API](#1-backend-api)
  - [2. Admin console](#2-admin-console)
  - [3. Public catalogue](#3-public-catalogue)
- [Environment variables](#environment-variables)
- [Migration commands](#migration-commands)
- [Seed admin credentials](#seed-admin-credentials)
- [API endpoints](#api-endpoints)
- [Troubleshooting](#troubleshooting)

---

## Repository layout

```
Techno-Hub/
├── backend/
│   ├── TechnoHub.sln
│   ├── Directory.Build.props          shared TFM + compiler settings
│   ├── Directory.Packages.props       central package versions
│   └── src/
│       ├── TechnoHub.Domain/          entities, roles, the scope catalogue
│       ├── TechnoHub.Application/     DTOs, service interfaces, FluentValidation
│       ├── TechnoHub.Infrastructure/  EF Core, Identity, auth services, seeder
│       └── TechnoHub.Api/             controllers, policies, Swagger, Program.cs
├── frontend-admin/                    Next.js 14 staff console (App Router, TS, Tailwind, shadcn/ui)
└── frontend-catalog/                  Next.js 14 public site — scaffold only, no auth
```

Dependencies flow inward only: `Api → Infrastructure → Application → Domain`.

> **A note on the target framework.** The spec called for .NET 8, but this machine has no
> `Microsoft.AspNetCore.App` 8.x shared runtime installed (only 10.0.x), so a `net8.0` web API would
> compile but not run. The solution targets **net10.0** with EF Core 10 instead, which matches the
> installed runtime and the `dotnet-ef` tooling. Nothing else in the design changed.

---

## The authorization model

This is the foundation every later module builds on, so it is worth reading once.

**There is no customer login anywhere in this system.** The public site is fully anonymous — visitors
browse the catalogue and build quotations without an account. Authentication exists only for internal
staff, and there is no public self-registration endpoint.

### Roles (fixed, three)

| Role | Meaning |
| --- | --- |
| `Admin` | Full access. **Bypasses every scope check** and holds no scope rows at all. |
| `Technician` | Created with `repairs.view` + `repairs.manage` as ordinary, revocable grants. |
| `User` | General staff. **Zero permissions** until an Admin assigns scopes. |

### Scopes (21, independent of role)

Permissions are scope strings held per user in a `UserScopes` join table. A Technician and a User with
the same scopes can do exactly the same things — the role only matters for `Admin`.

```
inventory.view      inventory.manage
suppliers.view      suppliers.manage
sales.view          sales.manage
quotations.view     quotations.manage
invoices.view       invoices.manage
customers.view      customers.manage
reports.view
staff.view          staff.manage
repairs.view        repairs.manage
warranty.view       warranty.manage
catalog.manage
notifications.manage
```

The catalogue is defined once, in
[`ScopeNames.cs`](backend/src/TechnoHub.Domain/Constants/ScopeNames.cs). The seeder mirrors it into
the `Scopes` table, the authorization policies are registered from it, and `GET /api/staff/scopes`
serves it to the admin UI — so a scope cannot exist in one place and not another.

### How a request is authorized

Scopes are baked into the JWT at login, so no database round trip happens on the request path.
[`ScopeAuthorizationHandler`](backend/src/TechnoHub.Api/Authorization/ScopeAuthorizationHandler.cs)
checks, in order:

1. **`type` claim is `staff`** — asserted before role or scopes are even looked at. Every issued token
   carries it, which future-proofs the token structure if a second identity space is ever added.
2. **`Admin` role** → pass, unconditionally.
3. **A literal matching `scope` claim** → pass. Otherwise 403.

Every route also lives under `/api/staff/**`, so when the public quotation module arrives under
`/api/public/**` the two are separated at the route level as well as by claims.

### Token lifetimes

- **Access token:** 45 minutes by default (`Jwt:AccessTokenMinutes`). Kept short because scopes are
  baked in — this is the worst-case window in which a permission change has not yet applied.
- **Refresh token:** 7 days, stored in the database as a SHA-256 hash, and **rotated on every use**.
  Presenting an already-used token is treated as theft and revokes every live session for that
  account.
- Changing a user's scopes or deactivating them **revokes their refresh tokens immediately**, which
  caps the staleness window at one access-token lifetime.

### Token storage in the browser

The admin app never puts a token in `localStorage`:

- The **access token lives in memory** (Zustand store, no `persist` middleware).
- The **refresh token lives in an httpOnly cookie** set by the app's own Next.js route handlers
  (`/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`). Those handlers are the only code that
  ever sees the refresh token; the browser cannot read it.
- Data calls go straight from the browser to the API with the access token as a `Bearer` header.
- After a page reload the in-memory token is gone, so `AuthProvider` calls `/api/auth/refresh` on
  first paint to rebuild the session from the cookie.

---

## Prerequisites

| Tool | Version used |
| --- | --- |
| .NET SDK | 10.0.x |
| `dotnet-ef` | 10.0.x — `dotnet tool install --global dotnet-ef` |
| Node.js | 20+ (22.x used here) |
| PostgreSQL | Any 14+ instance. Supabase works as a plain Postgres connection string — **do not** use the Supabase client SDK; this connects through EF Core/Npgsql directly. |

---

## Running locally

### 1. Backend API

```bash
cd backend

# Secrets stay out of source control. User-secrets is per-developer and per-project.
cd src/TechnoHub.Api

dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=db.<project>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<db-password>;SSL Mode=Require;Trust Server Certificate=true"

dotnet user-secrets set "Jwt:SigningKey"     "<at least 32 characters of random secret>"
dotnet user-secrets set "SeedAdmin:Email"    "admin@technohub.lk"
dotnet user-secrets set "SeedAdmin:Password" "<a strong password>"
dotnet user-secrets set "SeedAdmin:FullName" "System Administrator"

cd ../..
dotnet build TechnoHub.sln
dotnet run --project src/TechnoHub.Api
```

The API starts on **<http://localhost:5080>** and opens Swagger at **<http://localhost:5080/swagger>**.

In `Development`, `Database:ApplyMigrationsOnStartup` is `true`, so the schema is created on first run
and the roles, scope catalogue and root Admin are seeded. Health check: `GET /health`.

**Trying it in Swagger:** call `POST /api/staff/auth/login` with the seed credentials, copy
`accessToken` from the response, click **Authorize**, paste it. Each endpoint's description states the
role or scope it needs. There is also a ready-made request collection at
[`TechnoHub.Api.http`](backend/src/TechnoHub.Api/TechnoHub.Api.http).

### 2. Admin console

```bash
cd frontend-admin
cp .env.local.example .env.local     # defaults already point at http://localhost:5080
npm install
npm run dev
```

Opens on **<http://localhost:3000>**. Sign in with the seed Admin credentials.

- `/login` — sign-in form
- `/dashboard` — protected shell showing your role and scopes
- `/dashboard/staff` — **Admin-only**: create accounts and assign scopes via checkboxes grouped by
  module, then edit or deactivate existing accounts

### 3. Public catalogue

```bash
cd frontend-catalog
npm install
npm run dev
```

Opens on **<http://localhost:3001>** with a placeholder homepage. No auth setup at all — this app has
no session handling and never calls `/api/staff/**`.

---

## Environment variables

Configuration uses the standard .NET `Section__Key` convention, so anything below can come from
user-secrets, environment variables or a deployment secret store.

### Backend (`backend/src/TechnoHub.Api`)

| Variable | Required | Default | Notes |
| --- | --- | --- | --- |
| `ConnectionStrings__Default` | **yes** | — | Postgres connection string. Startup fails without it. |
| `Jwt__SigningKey` | **yes** | — | ≥ 32 bytes. Validated at startup, not on first login. |
| `Jwt__Issuer` | no | `TechnoHub.Api` | |
| `Jwt__Audience` | no | `TechnoHub.Staff` | |
| `Jwt__AccessTokenMinutes` | no | `45` | Must be 1–240. |
| `Jwt__RefreshTokenDays` | no | `7` | |
| `SeedAdmin__Email` | for first run | — | Without it **no Admin is created** and you cannot get in. |
| `SeedAdmin__Password` | for first run | — | Never hardcoded; never overwrites an existing account. |
| `SeedAdmin__UserName` | no | the email | |
| `SeedAdmin__FullName` | no | `System Administrator` | |
| `Database__ApplyMigrationsOnStartup` | no | `false` (`true` in Development) | |
| `Database__SeedOnStartup` | no | `true` | Roles + scopes + Admin. Idempotent. |
| `Cors__AllowedOrigins__0` | no | `http://localhost:3000` in Development | One entry per origin. |
| `Swagger__Enabled` | no | `false` (`true` in Development) | |
| `RefreshCookie__Secure` | no | `true` (`false` in Development) | Must be `false` for plain-http local dev. |
| `RefreshCookie__SameSite` | no | `Strict` (`Lax` in Development) | |

### Admin console (`frontend-admin/.env.local`)

| Variable | Default | Notes |
| --- | --- | --- |
| `BACKEND_API_URL` | `http://localhost:5080` | **Server-side only.** Used by the route handlers that hold the refresh token. |
| `NEXT_PUBLIC_API_BASE_URL` | `http://localhost:5080` | Sent to the browser, which calls the API directly with the access token. |
| `SESSION_COOKIE_NAME` | `th_admin_session` | Name of this app's httpOnly refresh cookie. |

---

## Migration commands

Run from `backend/`. `TechnoHub.Infrastructure` owns the migrations; `TechnoHub.Api` is the startup
project that supplies configuration.

```bash
# Create the initial migration (already committed as InitialStaffAuth)
dotnet ef migrations add InitialStaffAuth \
  --project src/TechnoHub.Infrastructure \
  --startup-project src/TechnoHub.Api

# Apply pending migrations
dotnet ef database update \
  --project src/TechnoHub.Infrastructure \
  --startup-project src/TechnoHub.Api

# Roll back to a previous migration
dotnet ef database update <PreviousMigrationName> \
  --project src/TechnoHub.Infrastructure \
  --startup-project src/TechnoHub.Api

# Remove the last (unapplied) migration
dotnet ef migrations remove \
  --project src/TechnoHub.Infrastructure \
  --startup-project src/TechnoHub.Api

# Inspect what would run, without touching the database
dotnet ef migrations script \
  --project src/TechnoHub.Infrastructure \
  --startup-project src/TechnoHub.Api
```

`dotnet ef` reads the same configuration the API does, so the connection string must be resolvable —
set it in user-secrets for `TechnoHub.Api`, or export `ConnectionStrings__Default` in the shell.

### Tables created

| Table | Purpose |
| --- | --- |
| `StaffUsers` | Identity users + `FullName`, `IsActive`, `CreatedAt`, `LastLoginAt`, `CreatedByUserId` |
| `StaffRoles` | `Admin`, `Technician`, `User` |
| `StaffUserRoles`, `StaffUserClaims`, `StaffUserLogins`, `StaffUserTokens`, `StaffRoleClaims` | Identity plumbing |
| `Scopes` | The 21-entry permission catalogue, keyed by scope string |
| `UserScopes` | Many-to-many user ↔ scope, with `GrantedAt` / `GrantedByUserId` |
| `RefreshTokens` | Hashed tokens with expiry, revocation reason and rotation chain |

---

## Seed admin credentials

There are **no hardcoded credentials in the repository.** The seeder reads
`SeedAdmin__Email` / `SeedAdmin__Password` and:

- creates the Admin only if that email does not already exist;
- **never** resets an existing account's password from configuration;
- logs a warning and creates no Admin at all if the values are missing — roles and scopes are still
  seeded, but you will not be able to sign in.

So the credentials are whatever you set in
[step 1](#1-backend-api). To rotate the password afterwards, change it through the application rather
than through configuration.

---

## API endpoints

All under `/api/staff`. Every response error is `application/problem+json` with an `errorCode` and a
`traceId`.

| Method | Route | Auth | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/staff/auth/login` | anonymous | Email **or** username + password → access token, refresh cookie, role and scopes |
| `POST` | `/api/staff/auth/refresh` | anonymous (cookie) | Rotate the refresh token, re-read scopes, issue a new access token |
| `POST` | `/api/staff/auth/logout` | anonymous (cookie) | Revoke the refresh token. Idempotent |
| `POST` | `/api/staff/auth/register` | **Admin** | Create a Technician or User, with initial scopes |
| `GET` | `/api/staff/auth/me` | any staff | Current profile, role and scopes, read fresh from the DB |
| `GET` | `/api/staff/scopes` | any staff | The scope catalogue, grouped by module |
| `GET` | `/api/staff/scopes/flat` | any staff | The scope catalogue, flat |
| `GET` | `/api/staff/users` | `staff.view` | List all staff accounts |
| `GET` | `/api/staff/users/{id}` | `staff.view` | One staff account |
| `PATCH` | `/api/staff/users/{id}/scopes` | **Admin** | Replace the account's scope set (absolute, not a delta) |
| `PATCH` | `/api/staff/users/{id}/status` | **Admin** | Activate / soft-disable. Never hard-deletes |
| `GET` | `/health` | anonymous | Liveness |

### Guard rails worth knowing

- `register` refuses to create an `Admin`, so the endpoint cannot be used to escalate.
- `PATCH .../scopes` on an Admin returns 409 — Admins bypass scope checks, so scope rows would be
  meaningless.
- `PATCH .../status` refuses to deactivate **your own account** or **the last active Admin**.
- Login reports "deactivated" only *after* the password verifies, so the endpoint cannot be used to
  enumerate which staff accounts exist.
- Failed logins feed Identity's lockout counter: 5 attempts, 15-minute lockout.

---

## Troubleshooting

**`Jwt:SigningKey is missing or shorter than 32 bytes`**
Set it via user-secrets or `Jwt__SigningKey`. The check is deliberately at startup so a misconfigured
deployment fails immediately rather than on the first login.

**`ConnectionStrings:Default is not configured`**
The API has no fallback database. Set it for the `TechnoHub.Api` project — `dotnet ef` reads the same
configuration, so this fixes migrations too.

**No Admin account exists after first run**
`SeedAdmin__Email` / `SeedAdmin__Password` were unset. Set both and restart; the seeder is idempotent.

**Login works in Swagger but the admin console can't sign in**
Check `Cors__AllowedOrigins` contains `http://localhost:3000`, and that `BACKEND_API_URL` in
`.env.local` points at the right port.

**Signed out immediately after signing in, or after a page refresh**
`RefreshCookie__Secure` must be `false` when serving plain http locally — a `Secure` cookie is
discarded by the browser over http. It is already `false` in `appsettings.Development.json`.

**Everything returns 403 for a newly created `User` account**
That is the intended behaviour: the `User` role starts with no scopes. Assign some from
`/dashboard/staff`.

**A permission change hasn't taken effect**
Scopes live in the access token. The change lands on the next refresh — the user's refresh tokens are
revoked when their scopes change, so they will pick it up within one access-token lifetime, or
immediately if they sign in again.
