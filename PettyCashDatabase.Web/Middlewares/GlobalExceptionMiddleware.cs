using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace PettyCashDatabase.Web.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(context, ex, _environment);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        Exception ex,
        IHostEnvironment environment)
    {
        if (context.Response.HasStarted)
            throw ex;

        var (status, title) = ex switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Bad Request"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Not Found"),
            _ => (HttpStatusCode.InternalServerError, "An error occurred")
        };

        // Keep the temporary lockout contract consistent for both API requests
        // and Interactive Server calls that are translated by the server adapter.
        if (ex is AccountLockedException)
        {
            status = HttpStatusCode.Locked;
            title = ex.Message;
        }
        else if (ex is TamperDetectedException)
        {
            status = HttpStatusCode.Forbidden;
            title = "Forbidden";
        }

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = status == HttpStatusCode.InternalServerError && !environment.IsDevelopment()
                ? "An unexpected error occurred."
                : ex.GetBaseException().Message,
            Instance = context.Request.Path
        };

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? 500;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}

public sealed class AccountLockedException : Exception
{
    public AccountLockedException(string message = "Account temporarily locked. Try again in 15 minutes.")
        : base(message)
    {
    }
}

public sealed class TamperDetectedException : Exception
{
    public TamperDetectedException()
        : base("Account integrity check failed.")
    {
    }
}
