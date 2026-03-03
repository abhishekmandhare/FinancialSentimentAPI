using Application.Exceptions;
using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace API.Middleware;

/// <summary>
/// Single place for all exception → HTTP status code mapping.
/// Returns RFC 7807 ProblemDetails — standard format, tooling-friendly.
///
/// Middleware order matters: register this before all other middleware
/// so it catches exceptions from anywhere in the pipeline.
///
/// ValidationException  → 422 Unprocessable Entity  (client sent bad data)
/// NotFoundException    → 404 Not Found             (resource doesn't exist)
/// DomainException      → 400 Bad Request           (business rule violated)
/// Everything else      → 500 Internal Server Error (our fault)
/// </summary>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Application.Exceptions.ValidationException ex)
        {
            logger.LogWarning("Validation failure: {Errors}", ex.Errors);
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity,
                "Validation Error", ex.Message, ex.Errors);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning("Resource not found: {Message}", ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status404NotFound,
                "Not Found", ex.Message);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Domain rule violated: {Message}", ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                "Business Rule Violation", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Internal Server Error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        object? extensions = null)
    {
        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title  = title,
            Detail = detail,
            Type   = $"https://httpstatuses.io/{statusCode}"
        };

        if (extensions is not null)
            problem.Extensions["errors"] = extensions;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
