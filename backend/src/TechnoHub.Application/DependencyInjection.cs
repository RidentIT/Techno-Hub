using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TechnoHub.Application.Scopes;

namespace TechnoHub.Application;

/// <summary>Registration for the Application layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Picks up every AbstractValidator in this assembly. Validators are stateless, so a
        // singleton lifetime avoids rebuilding the rule trees on every request.
        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly,
            ServiceLifetime.Singleton);

        // The scope catalogue is a compile-time constant list, so a singleton is enough.
        services.AddSingleton<IScopeCatalogService, ScopeCatalogService>();

        return services;
    }
}
