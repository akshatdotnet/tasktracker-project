using System.Net;
using System.Text.Json;
using TaskTracker.Application.Common.Models;

namespace TaskTracker.API.Middleware;

/// <summary>
/// Global exception middleware — catches ALL unhandled exceptions and returns
/// a uniform ErrorResponse JSON. Prevents stack traces leaking to clients.
///
/// OWASP: Never expose internal exception details in API responses.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next   = next;
        _logger = logger;
        _env    = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ctx, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext ctx, Exception ex)
    {
        var traceId = ctx.TraceIdentifier;
        _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", traceId);

        ctx.Response.ContentType = "application/json";

        // Never expose internal details in production
        var message = _env.IsDevelopment()
            ? ex.Message
            : "An unexpected error occurred. Please try again later.";

        var (statusCode, code) = ex switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,  "UNAUTHORIZED"),
            ArgumentException           => (HttpStatusCode.BadRequest,     "BAD_REQUEST"),
            KeyNotFoundException        => (HttpStatusCode.NotFound,       "NOT_FOUND"),
            InvalidOperationException   => (HttpStatusCode.Conflict,       "CONFLICT"),
            _                           => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR")
        };

        ctx.Response.StatusCode = (int)statusCode;

        var error    = new ErrorResponse(message, code, traceId);
        var json     = JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await ctx.Response.WriteAsync(json);
    }
}
