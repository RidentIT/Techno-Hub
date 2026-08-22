using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TechnoHub.Application.Common.Exceptions;

namespace TechnoHub.Api.Middleware;

/// <summary>
/// Turns exceptions into ProblemDetails responses so the frontend always gets the same error shape.
/// </summary>
/// <remarks>
/// <see cref="AppException"/> and friends are expected outcomes — a bad password, a duplicate email —
/// so they are logged at information/warning level with their status code. Anything else is a bug:
/// it is logged as an error with the full stack trace, and the client is told nothing beyond
/// "unexpected error" plus a trace id to quote.
/// </remarks>
public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationFailedException exception)
        {
            _logger.LogInformation(
                "Validation failed for {Method} {Path}: {Errors}",
                context.Request.Method, context.Request.Path, exception.Errors);

            await WriteValidationProblemAsync(context, exception);
        }
        catch (AppException exception)
        {
            _logger.LogWarning(
                "{ExceptionType} on {Method} {Path}: {Message}",
                exception.GetType().Name, context.Request.Method, context.Request.Path, exception.Message);

            await WriteProblemAsync(
                context,
                exception.StatusCode,
                TitleFor(exception.StatusCode),
                exception.Message,
                exception.ErrorCode);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client hung up. Not an error, and there is nobody left to send a response to.
            _logger.LogInformation("Request {Method} {Path} was aborted by the client.",
                context.Request.Method, context.Request.Path);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}.",
                context.Request.Method, context.Request.Path);

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                _environment.IsDevelopment()
                    ? exception.ToString()
                    : "An unexpected error occurred. Quote the traceId when reporting this.",
                "internal_error");
        }
    }

    private static async Task WriteValidationProblemAsync(HttpContext context, ValidationFailedException exception)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ValidationProblemDetails(
            exception.Errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
        {
            Status = exception.StatusCode,
            Title = "Validation failed",
            Detail = exception.Message,
            Instance = context.Request.Path,
        };

        problem.Extensions["errorCode"] = exception.ErrorCode;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await WriteAsync(context, exception.StatusCode, problem);
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string errorCode)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };

        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await WriteAsync(context, statusCode, problem);
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, object problem)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, SerializerOptions));
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Request failed",
    };
}
