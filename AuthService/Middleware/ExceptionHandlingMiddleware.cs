using System.Net;
using System.Text.Json;
using MySqlConnector;

namespace AuthService.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/problem+json";

            var payload = new
            {
                type = "about:blank",
                title = ex.Code,
                status = ex.StatusCode,
                detail = ex.Message,
                instance = context.Request.Path.Value ?? string.Empty,
                code = ex.Code
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            if (TryMapDatabaseException(ex, out var mappedStatus, out var mappedCode, out var mappedDetail))
            {
                context.Response.StatusCode = mappedStatus;
                context.Response.ContentType = "application/problem+json";

                var mappedPayload = new
                {
                    type = "about:blank",
                    title = mappedCode,
                    status = mappedStatus,
                    detail = mappedDetail,
                    instance = context.Request.Path.Value ?? string.Empty,
                    code = mappedCode
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(mappedPayload));
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var payload = new
            {
                type = "about:blank",
                title = "server_error",
                status = 500,
                detail = "An unexpected error occurred.",
                instance = context.Request.Path.Value ?? string.Empty,
                code = "server_error"
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }

    private static bool TryMapDatabaseException(Exception ex, out int status, out string code, out string detail)
    {
        status = 0;
        code = string.Empty;
        detail = string.Empty;

        var mysql = FindMySqlException(ex);
        if (mysql == null)
        {
            return false;
        }

        if (mysql.Number == 1045)
        {
            status = (int)HttpStatusCode.ServiceUnavailable;
            code = "database_auth_failed";
            detail = "Database authentication failed. Check AuthDb credentials.";
            return true;
        }

        status = (int)HttpStatusCode.ServiceUnavailable;
        code = "database_unavailable";
        detail = "Database is unavailable.";
        return true;
    }

    private static MySqlException? FindMySqlException(Exception ex)
    {
        Exception? current = ex;
        while (current != null)
        {
            if (current is MySqlException mysql)
            {
                return mysql;
            }

            current = current.InnerException;
        }

        return null;
    }
}
