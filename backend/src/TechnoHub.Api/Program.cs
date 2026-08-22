using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using TechnoHub.Api.Authorization;
using TechnoHub.Api.Filters;
using TechnoHub.Api.Middleware;
using TechnoHub.Api.Swagger;
using TechnoHub.Application;
using TechnoHub.Application.Common.Options;
using TechnoHub.Infrastructure;
using TechnoHub.Infrastructure.Persistence.Seeding;

// Minimal logger for anything that goes wrong before configuration is read.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ---------------------------------------------------------------------------------------------
    // Configuration
    // ---------------------------------------------------------------------------------------------

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                     ?? new JwtOptions();

    // `dotnet ef` builds this host to read the EF model, so Program.cs must not demand runtime
    // secrets it does not need for that. Without this, adding a migration would require a
    // throwaway Jwt__SigningKey on the command line.
    var isDesignTime = string.Equals(
        Assembly.GetEntryAssembly()?.GetName().Name, "ef", StringComparison.Ordinal);

    if (!isDesignTime)
    {
        // Asserted eagerly so a misconfigured deployment dies at startup rather than on somebody's
        // first login. Building the SymmetricSecurityKey below would otherwise fail with an opaque
        // "key length is zero".
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
            || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or shorter than 32 bytes. Set it with " +
                "`dotnet user-secrets set \"Jwt:SigningKey\" \"<32+ character secret>\"` or the " +
                "Jwt__SigningKey environment variable.");
        }

        if (jwtOptions.AccessTokenMinutes is <= 0 or > 240)
        {
            throw new InvalidOperationException(
                "Jwt:AccessTokenMinutes must be between 1 and 240. Scopes are baked into the access " +
                "token, so a long lifetime means permission changes take a long time to apply.");
        }

        if (jwtOptions.RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("Jwt:RefreshTokenDays must be greater than zero.");
        }
    }

    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? Array.Empty<string>();

    // ---------------------------------------------------------------------------------------------
    // Services
    // ---------------------------------------------------------------------------------------------

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddStaffAuthorization();
    builder.Services.AddTechnoHubSwagger();

    builder.Services.AddControllers(options =>
        {
            // Global, so every endpoint with a registered validator is checked without an attribute.
            options.Filters.Add<ValidationFilter>();
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // IMiddleware implementations are resolved from DI per request.
    builder.Services.AddScoped<ExceptionHandlingMiddleware>();

    builder.Services.AddHealthChecks();

    // Model-binding failures (malformed JSON, a non-Guid route value) never reach the validation
    // filter, so give them the same ProblemDetails shape the filter produces.
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "One or more validation errors occurred.",
                Instance = context.HttpContext.Request.Path,
            };

            problem.Extensions["errorCode"] = "validation_failed";
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            return new BadRequestObjectResult(problem)
            {
                ContentTypes = { "application/problem+json" },
            };
        };
    });

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                return;
            }

            // AllowCredentials is required for the refresh cookie, and cannot be combined with a
            // wildcard origin — hence the explicit list from configuration.
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Leave claim types exactly as the token carries them: "sub" stays "sub" rather than
            // being rewritten to the long WS-Fed URI, which is what ClaimsPrincipalExtensions reads.
            options.MapInboundClaims = false;
            options.SaveToken = false;
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),

                // Match the short claim types minted by TokenService, so User.IsInRole and
                // User.Identity.Name work without any mapping.
                NameClaimType = "name",
                RoleClaimType = "role",
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    // Lets the frontend tell "refresh me" apart from "this token is garbage".
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Append("x-token-expired", "true");
                    }

                    return Task.CompletedTask;
                },

                OnChallenge = async context =>
                {
                    // Default behaviour is an empty 401 body; emit ProblemDetails instead so every
                    // error the API returns has the same shape.
                    context.HandleResponse();

                    if (context.Response.HasStarted)
                    {
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Unauthorized",
                        Detail = string.IsNullOrEmpty(context.ErrorDescription)
                            ? "A valid staff access token is required."
                            : context.ErrorDescription,
                        Instance = context.Request.Path,
                    };

                    problem.Extensions["errorCode"] =
                        context.Response.Headers.ContainsKey("x-token-expired")
                            ? "token_expired"
                            : "unauthorized";
                    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                },

                OnForbidden = async context =>
                {
                    if (context.Response.HasStarted)
                    {
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "Your account does not have the role or scope this endpoint requires.",
                        Instance = context.Request.Path,
                    };

                    problem.Extensions["errorCode"] = "insufficient_scope";
                    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                },
            };
        });

    var app = builder.Build();

    // ---------------------------------------------------------------------------------------------
    // Pipeline
    // ---------------------------------------------------------------------------------------------

    // First, so it can catch anything thrown further down.
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSerilogRequestLogging();

    var swaggerEnabled = app.Environment.IsDevelopment()
                         || app.Configuration.GetValue("Swagger:Enabled", false);

    if (swaggerEnabled)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Techno Hub Staff API v1");
            options.DocumentTitle = "Techno Hub — Staff API";
            options.DisplayRequestDuration();
        });
    }

    if (!app.Environment.IsDevelopment())
    {
        // Skipped locally: the dev profile serves plain http, and redirecting would break the
        // refresh cookie round trip during development.
        app.UseHttpsRedirection();
    }

    app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    // ---------------------------------------------------------------------------------------------
    // Database migration + seeding
    // ---------------------------------------------------------------------------------------------

    await using (var scope = app.Services.CreateAsyncScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

        if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
        {
            await seeder.MigrateAsync();
        }

        if (app.Configuration.GetValue("Database:SeedOnStartup", true))
        {
            await seeder.SeedAsync();
        }
    }

    Log.Information("Techno Hub Staff API starting in {Environment}.", app.Environment.EnvironmentName);

    await app.RunAsync();
}
catch (HostAbortedException)
{
    // Thrown by design when `dotnet ef` builds the host to read the model, then stops it.
    // Not a failure, so it must not be logged as fatal — but it does have to propagate for the
    // tooling to work.
    throw;
}
catch (Exception exception)
{
    Log.Fatal(exception, "Techno Hub Staff API terminated unexpectedly during startup.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
