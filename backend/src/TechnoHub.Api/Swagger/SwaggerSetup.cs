using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using TechnoHub.Api.Authorization;
using TechnoHub.Domain.Constants;

namespace TechnoHub.Api.Swagger;

/// <summary>Swagger/OpenAPI configuration, including the bearer-token flow in the UI.</summary>
public static class SwaggerSetup
{
    internal const string BearerSchemeId = "Bearer";

    public static IServiceCollection AddTechnoHubSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Techno Hub — Staff API",
                Version = "v1",
                Description =
                    "Internal staff API for the Techno Hub business management system.\n\n" +
                    "**Authentication.** Every route lives under `/api/staff/**` and needs a bearer " +
                    "token issued by `POST /api/staff/auth/login`. There is no customer or public " +
                    "login anywhere in this system; the public catalogue and quotation builder are " +
                    "anonymous and will live under `/api/public/**`.\n\n" +
                    "**Authorization.** A token carries a role (Admin, Technician or User) and one " +
                    "`scope` claim per granted permission. The Admin role satisfies every scope " +
                    "check. Everyone else needs the exact scope listed on the endpoint.\n\n" +
                    "**To try it out:** call `login`, copy the `accessToken` from the response, then " +
                    "click **Authorize** and paste it.",
            });

            options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Paste the raw JWT from the login response. Swagger adds the `Bearer ` prefix " +
                    "for you.",
            });

            // Only endpoints that actually require auth get the padlock and the header.
            options.OperationFilter<AuthorizationOperationFilter>();

            var xmlPath = Path.Combine(
                AppContext.BaseDirectory,
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            // Pulls the DTO doc comments from the Application layer into the schema descriptions.
            foreach (var assemblyName in new[] { "TechnoHub.Application", "TechnoHub.Domain" })
            {
                var path = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml");
                if (File.Exists(path))
                {
                    options.IncludeXmlComments(path);
                }
            }

            options.SupportNonNullableReferenceTypes();
        });

        return services;
    }
}

/// <summary>
/// Marks up each operation with the auth it needs: adds the bearer security requirement and the
/// 401/403 responses, and states the required role or scope in the description.
/// </summary>
/// <remarks>
/// Reading this off the endpoint's real authorization metadata means the docs cannot drift from the
/// policies — an endpoint's padlock and its stated scope come from the same <c>[Authorize]</c>
/// attribute the runtime enforces.
/// </remarks>
public sealed class AuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var authorizeAttributes = metadata.OfType<AuthorizeAttribute>().ToList();
        if (authorizeAttributes.Count == 0)
        {
            return;
        }

        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "No token, or the token is expired, malformed or not a staff token.",
        });

        operation.Responses.TryAdd("403", new OpenApiResponse
        {
            Description = "Authenticated, but the token lacks the required role or scope.",
        });

        var policies = authorizeAttributes
            .Select(attribute => attribute.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .Select(policy => policy!)
            .ToList();

        var note = DescribeRequirement(policies);
        if (note is not null)
        {
            operation.Description = string.IsNullOrWhiteSpace(operation.Description)
                ? note
                : $"{operation.Description}\n\n{note}";
        }

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = SwaggerSetup.BearerSchemeId,
                },
            }] = Array.Empty<string>(),
        });
    }

    private static string? DescribeRequirement(IReadOnlyList<string> policies)
    {
        if (policies.Count == 0)
        {
            return "**Requires:** any valid staff token.";
        }

        if (policies.Contains(AuthorizationPolicyNames.AdminOnly))
        {
            return "**Requires:** the `Admin` role. Scopes do not grant access to this endpoint.";
        }

        if (policies.Contains(AuthorizationPolicyNames.StaffOnly))
        {
            return "**Requires:** any valid staff token.";
        }

        // Every remaining policy name is a scope key, because that is how they are registered.
        var scopes = policies.Where(ScopeNames.IsValid).ToList();

        return scopes.Count > 0
            ? $"**Requires scope:** {string.Join(", ", scopes.Select(s => $"`{s}`"))} " +
              "(the `Admin` role bypasses this)."
            : null;
    }
}
