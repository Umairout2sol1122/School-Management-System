using System.Net;
using System.Text.Json;
using Serilog;
using SchoolManagement.Application.Common.Exceptions;

namespace SchoolManagement.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        string correlationId = Guid.NewGuid().ToString("N");
        context.Response.ContentType = "application/json";

        var (statusCode, message, errors) = exception switch
        {
            CrossTenantAccessException crossTenantEx => (
                (int)HttpStatusCode.Forbidden,
                crossTenantEx.Message,
                (object?)null),

            UnauthorizedException unauthorizedEx => (
                (int)HttpStatusCode.Unauthorized,
                unauthorizedEx.Message,
                (object?)null),

            ValidationException validationEx => (
                (int)HttpStatusCode.BadRequest,
                "Validation failed.",
                validationEx.Errors),

            NotFoundException notFoundEx => (
                (int)HttpStatusCode.NotFound,
                notFoundEx.Message,
                (object?)null),

            _ => (
                (int)HttpStatusCode.InternalServerError,
                "An unexpected server error occurred. Please contact system support.",
                (object?)null)
        };

        context.Response.StatusCode = statusCode;

        if (statusCode >= 500)
        {
            Log.Error(exception, "[CorrelationId:{CorrelationId}] Unhandled server exception on {Path}", correlationId, context.Request.Path);
        }
        else
        {
            Log.Warning(exception, "[CorrelationId:{CorrelationId}] Client error ({StatusCode}) on {Path}: {Message}", correlationId, statusCode, context.Request.Path, message);
        }

        var responsePayload = new
        {
            success = false,
            message,
            errors,
            correlationId,
            timestampUtc = DateTime.UtcNow
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(responsePayload));
    }
}
