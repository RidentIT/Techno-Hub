using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechnoHub.Application.Auth;
using TechnoHub.Application.Common.Options;
using TechnoHub.Application.Staff;
using TechnoHub.Domain.Entities;
using TechnoHub.Infrastructure.Auth;
using TechnoHub.Infrastructure.Persistence;
using TechnoHub.Infrastructure.Persistence.Seeding;
using TechnoHub.Infrastructure.Staff;

namespace TechnoHub.Infrastructure;

/// <summary>Registration for the Infrastructure layer: database, Identity and the auth services.</summary>
public static class DependencyInjection
{
    /// <summary>Minimum signing-key length in bytes for HMAC-SHA256.</summary>
    private const int MinimumSigningKeyBytes = 32;

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // These rules are asserted eagerly in Program.cs too, so a misconfigured deployment fails at
        // startup rather than on somebody's first login attempt. They are repeated here so that any
        // other host of this layer (a worker, a test fixture) gets the same guarantees.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SigningKey),
                "Jwt:SigningKey is not configured. Set the Jwt__SigningKey environment variable or " +
                "use `dotnet user-secrets set Jwt:SigningKey \"<32+ char secret>\"`.")
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.SigningKey) >= MinimumSigningKeyBytes,
                $"Jwt:SigningKey must be at least {MinimumSigningKeyBytes} bytes for HMAC-SHA256.")
            .Validate(
                options => options.AccessTokenMinutes is > 0 and <= 240,
                "Jwt:AccessTokenMinutes must be between 1 and 240. Scopes are baked into the access " +
                "token, so a long lifetime means permission changes take a long time to apply.")
            .Validate(
                options => options.RefreshTokenDays > 0,
                "Jwt:RefreshTokenDays must be greater than zero.");

        services.AddOptions<SeedAdminOptions>()
            .Bind(configuration.GetSection(SeedAdminOptions.SectionName));

        services.AddOptions<RefreshCookieOptions>()
            .Bind(configuration.GetSection(RefreshCookieOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Point it at your PostgreSQL instance " +
                "(Supabase works as a plain Postgres connection string).");
        }

        services.AddDbContext<TechnoHubDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(TechnoHubDbContext).Assembly.FullName);

                // The database is a hosted instance across the network, so brief connection blips
                // are normal and worth retrying rather than surfacing as a 500.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });
        });

        // AddIdentityCore rather than AddIdentity: we authenticate with bearer tokens, and AddIdentity
        // would also register cookie authentication schemes that nothing in this API uses.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.User.RequireUniqueEmail = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                // Accounts are created by an Admin who already knows the person; there is no
                // confirmation email to send.
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<TechnoHubDbContext>();

        // No AddDefaultTokenProviders(): those providers exist to mint email-confirmation and
        // password-reset tokens, neither of which this module has. Adding them would also drag the
        // ASP.NET Core Identity assembly into this layer. A later module that needs password reset
        // should register the specific provider it wants via AddTokenProvider.

        // Stateless and cheap to construct once; holds the parsed signing key.
        services.AddSingleton<ITokenService, TokenService>();

        services.AddScoped<StaffUserReader>();
        services.AddScoped<RefreshTokenRevoker>();
        services.AddScoped<IStaffAuthService, StaffAuthService>();
        services.AddScoped<IStaffUserService, StaffUserService>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
