/**
 * Scope keys, mirroring TechnoHub.Domain.Constants.ScopeNames.
 *
 * The assignment UI does not read this list — it fetches the catalogue from
 * GET /api/staff/scopes so the checkboxes can never offer a scope the backend rejects. These
 * constants exist for `hasScope(...)` calls in code, where a typo would otherwise silently hide a
 * button forever.
 */
export const SCOPES = {
  inventoryView: "inventory.view",
  inventoryManage: "inventory.manage",

  suppliersView: "suppliers.view",
  suppliersManage: "suppliers.manage",

  salesView: "sales.view",
  salesManage: "sales.manage",

  quotationsView: "quotations.view",
  quotationsManage: "quotations.manage",

  invoicesView: "invoices.view",
  invoicesManage: "invoices.manage",

  customersView: "customers.view",
  customersManage: "customers.manage",

  reportsView: "reports.view",

  staffView: "staff.view",
  staffManage: "staff.manage",

  repairsView: "repairs.view",
  repairsManage: "repairs.manage",

  warrantyView: "warranty.view",
  warrantyManage: "warranty.manage",

  catalogManage: "catalog.manage",

  notificationsManage: "notifications.manage",
} as const;

export type ScopeKey = (typeof SCOPES)[keyof typeof SCOPES];
