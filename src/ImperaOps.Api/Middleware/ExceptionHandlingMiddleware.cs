using System.Text.Json;
using ImperaOps.Domain.Exceptions;

namespace ImperaOps.Api.Middleware;

/// <summary>
/// Catches domain exceptions thrown from any layer and maps them to the
/// appropriate HTTP status code + RFC 7807 problem details response.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            await HandleDomainExceptionAsync(context, ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private static async Task HandleDomainExceptionAsync(HttpContext context, DomainException ex)
    {
        var (status, detail) = ex switch
        {
            NotFoundException      => (StatusCodes.Status404NotFound,    ex.Message),
            ForbiddenException     => (StatusCodes.Status403Forbidden,   ex.Message),
            ConflictException      => (StatusCodes.Status409Conflict,    ex.Message),
            ValidationException ve => (StatusCodes.Status400BadRequest,  ve.Message),
            _                      => (StatusCodes.Status400BadRequest,  ex.Message),
        };

        if (ex is ValidationException v && v.Errors.Count > 0)
        {
            await WriteValidationProblemAsync(context, status, v);
            return;
        }

        await WriteProblemAsync(context, status, detail);
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            status,
            title = ReasonPhrase(status),
            detail,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOpts));
    }

    private static async Task WriteValidationProblemAsync(HttpContext context, int status, ValidationException ex)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            status,
            title = "Validation failed.",
            detail = ex.Message,
            errors = ex.Errors,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOpts));
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        _   => "Error",
    };
}
