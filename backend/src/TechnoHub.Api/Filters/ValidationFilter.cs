using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using TechnoHub.Application.Common.Exceptions;

namespace TechnoHub.Api.Filters;

/// <summary>
/// Runs the FluentValidation validator for every action argument that has one, before the action
/// body executes.
/// </summary>
/// <remarks>
/// Applied globally so a new endpoint gets validation by simply having a validator registered —
/// there is no per-action attribute to remember. Failures are raised as
/// <see cref="ValidationFailedException"/> so they render through the same ProblemDetails path as
/// every other error.
/// </remarks>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services)
    {
        _services = services;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (_services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                throw new ValidationFailedException(
                    result.ToDictionary().ToDictionary(kvp => ToCamelCase(kvp.Key), kvp => kvp.Value));
            }
        }

        await next();
    }

    /// <summary>
    /// FluentValidation reports property names as written in C# (<c>EmailOrUsername</c>); the JSON
    /// contract is camelCase, so the keys are lowered to match what the client actually sent.
    /// </summary>
    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
        {
            return propertyName;
        }

        // Handles nested paths such as "Scopes[0]" and "Address.Line1".
        var segments = propertyName.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length > 0 && char.IsUpper(segments[i][0]))
            {
                segments[i] = char.ToLowerInvariant(segments[i][0]) + segments[i][1..];
            }
        }

        return string.Join('.', segments);
    }
}
