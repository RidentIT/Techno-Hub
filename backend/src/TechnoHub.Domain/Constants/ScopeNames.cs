namespace TechnoHub.Domain.Constants;

/// <summary>
/// The fixed catalogue of permission strings. Scopes are granted per-user and are
/// completely independent of <see cref="RoleNames"/> — a Technician and a User can hold
/// exactly the same scopes. The one exception is <see cref="RoleNames.Admin"/>, which
/// passes every scope check without holding any rows in UserScopes.
///
/// These constants are the single source of truth: the seeder mirrors <see cref="All"/>
/// into the Scopes table on startup, and the authorization policies are registered from
/// the same list, so a scope can never exist in one place and not the other.
/// </summary>
public static class ScopeNames
{
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";

    public const string SuppliersView = "suppliers.view";
    public const string SuppliersManage = "suppliers.manage";

    public const string SalesView = "sales.view";
    public const string SalesManage = "sales.manage";

    public const string QuotationsView = "quotations.view";
    public const string QuotationsManage = "quotations.manage";

    public const string InvoicesView = "invoices.view";
    public const string InvoicesManage = "invoices.manage";

    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";

    public const string ReportsView = "reports.view";

    public const string StaffView = "staff.view";
    public const string StaffManage = "staff.manage";

    public const string RepairsView = "repairs.view";
    public const string RepairsManage = "repairs.manage";

    public const string WarrantyView = "warranty.view";
    public const string WarrantyManage = "warranty.manage";

    public const string CatalogManage = "catalog.manage";

    public const string NotificationsManage = "notifications.manage";

    /// <summary>Module labels, used to group the checkboxes on the staff admin screen.</summary>
    public static class Modules
    {
        public const string Inventory = "Inventory";
        public const string Suppliers = "Suppliers";
        public const string Sales = "Sales";
        public const string Quotations = "Quotations";
        public const string Invoices = "Invoices";
        public const string Customers = "Customers";
        public const string Reports = "Reports";
        public const string Staff = "Staff";
        public const string Repairs = "Repairs";
        public const string Warranty = "Warranty";
        public const string Catalog = "Catalog";
        public const string Notifications = "Notifications";
    }

    /// <summary>Every scope, in the display order the admin UI should use.</summary>
    public static readonly IReadOnlyList<ScopeDefinition> All = new[]
    {
        new ScopeDefinition(InventoryView, Modules.Inventory, "View stock levels, products and stock movements"),
        new ScopeDefinition(InventoryManage, Modules.Inventory, "Create, edit and adjust products and stock"),

        new ScopeDefinition(SuppliersView, Modules.Suppliers, "View suppliers and purchase orders"),
        new ScopeDefinition(SuppliersManage, Modules.Suppliers, "Create and edit suppliers and purchase orders"),

        new ScopeDefinition(SalesView, Modules.Sales, "View sales orders and transaction history"),
        new ScopeDefinition(SalesManage, Modules.Sales, "Create, edit and void sales"),

        new ScopeDefinition(QuotationsView, Modules.Quotations, "View customer quotations and custom build requests"),
        new ScopeDefinition(QuotationsManage, Modules.Quotations, "Respond to, revise and convert quotations"),

        new ScopeDefinition(InvoicesView, Modules.Invoices, "View invoices and payment status"),
        new ScopeDefinition(InvoicesManage, Modules.Invoices, "Issue, edit and credit invoices"),

        new ScopeDefinition(CustomersView, Modules.Customers, "View customer records and contact details"),
        new ScopeDefinition(CustomersManage, Modules.Customers, "Create and edit customer records"),

        new ScopeDefinition(ReportsView, Modules.Reports, "View business and financial reports"),

        new ScopeDefinition(StaffView, Modules.Staff, "View staff accounts, roles and assigned scopes"),
        new ScopeDefinition(StaffManage, Modules.Staff, "Create staff accounts and change roles, scopes and status"),

        new ScopeDefinition(RepairsView, Modules.Repairs, "View repair and service jobs"),
        new ScopeDefinition(RepairsManage, Modules.Repairs, "Create, update and close repair and service jobs"),

        new ScopeDefinition(WarrantyView, Modules.Warranty, "View warranty registrations and claims"),
        new ScopeDefinition(WarrantyManage, Modules.Warranty, "Register warranties and process claims"),

        new ScopeDefinition(CatalogManage, Modules.Catalog, "Manage the public catalogue content and visibility"),

        new ScopeDefinition(NotificationsManage, Modules.Notifications, "Configure and send system notifications"),
    };

    /// <summary>Just the keys, for fast validation of an incoming scope list.</summary>
    public static readonly IReadOnlySet<string> AllKeys =
        All.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Scopes a newly created Technician is granted automatically. These are written as real
    /// UserScopes rows, so an Admin can revoke them later like any other scope — the Technician
    /// role itself grants nothing implicitly.
    /// </summary>
    public static readonly IReadOnlyList<string> TechnicianDefaults = new[]
    {
        RepairsView,
        RepairsManage,
    };

    public static bool IsValid(string? key) => key is not null && AllKeys.Contains(key);
}

/// <summary>A scope's identity, module grouping and human description.</summary>
public sealed record ScopeDefinition(string Key, string Module, string Description);
