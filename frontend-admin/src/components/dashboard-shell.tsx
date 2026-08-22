"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { LayoutDashboard, LogOut, Users } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAuth } from "@/lib/auth-context";
import { cn } from "@/lib/utils";

interface NavItem {
  href: string;
  label: string;
  icon: typeof LayoutDashboard;
  /** Scopes required to see the link. Empty means any staff member. */
  scopes: string[];
  /** When true, only the Admin role sees it regardless of scopes. */
  adminOnly?: boolean;
}

/**
 * Later modules add their entries here. Each one declares the scope it needs, and the sidebar
 * filters itself — the same rule the API enforces, so a staff member never sees a link that would
 * 403 on click.
 */
const NAV_ITEMS: NavItem[] = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard, scopes: [] },
  { href: "/dashboard/staff", label: "Staff & permissions", icon: Users, scopes: [], adminOnly: true },
];

export function DashboardShell({ children }: { children: React.ReactNode }) {
  const { user, role, logout, hasAllScopes, isAdmin } = useAuth();
  const pathname = usePathname();
  const router = useRouter();

  const visibleItems = NAV_ITEMS.filter((item) => {
    if (item.adminOnly && !isAdmin) return false;
    return hasAllScopes(...item.scopes);
  });

  async function handleLogout() {
    await logout();
    router.replace("/login");
  }

  return (
    <div className="flex min-h-screen flex-col">
      <header className="sticky top-0 z-10 border-b border-border bg-card">
        <div className="flex h-16 items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex items-center gap-3">
            <span className="text-lg font-bold tracking-tight">Techno Hub</span>
            <Badge variant="outline" className="hidden sm:inline-flex">
              Staff console
            </Badge>
          </div>

          <div className="flex items-center gap-3">
            <div className="hidden text-right sm:block">
              <p className="text-sm font-medium leading-tight">{user?.fullName}</p>
              <p className="text-xs text-muted-foreground">
                {user?.email} · {role}
              </p>
            </div>

            <Button variant="outline" size="sm" onClick={handleLogout}>
              <LogOut className="h-4 w-4" />
              <span className="hidden sm:inline">Sign out</span>
            </Button>
          </div>
        </div>
      </header>

      <div className="flex flex-1 flex-col md:flex-row">
        <nav className="border-b border-border bg-muted/30 p-3 md:w-64 md:border-b-0 md:border-r md:p-4">
          <ul className="flex gap-2 overflow-x-auto md:flex-col md:overflow-visible">
            {visibleItems.map((item) => {
              const active = pathname === item.href;
              const Icon = item.icon;

              return (
                <li key={item.href}>
                  <Link
                    href={item.href}
                    className={cn(
                      "flex items-center gap-2 whitespace-nowrap rounded-md px-3 py-2 text-sm font-medium transition-colors",
                      active
                        ? "bg-primary text-primary-foreground"
                        : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
                    )}
                  >
                    <Icon className="h-4 w-4" />
                    {item.label}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>

        <main className="flex-1 p-4 sm:p-6">{children}</main>
      </div>
    </div>
  );
}
