using System.Net;
using System.Text.Json;
using test_project.Models.DTO;

namespace test_project.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException ex)
        {
            _logger.LogWarning(ex, "JWT токен истек: {Message}", ex.Message);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var response = new { error = "Unauthorized", message = "Токен истек. Требуется повторная аутентификация." };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Ошибка валидации JWT токена: {Message}", ex.Message);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var response = new { error = "Unauthorized", message = "Невалидный токен." };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необработанная ошибка: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var statusCode = HttpStatusCode.InternalServerError;
        var error = "InternalServerError";
        var message = "Произошла внутренняя ошибка сервера";

        switch (exception)
        {
            case test_project.Sharding.ShardUnavailableException:
                statusCode = HttpStatusCode.ServiceUnavailable;
                error = "ShardUnavailable";
                message = exception.Message;
                break;

            case KeyNotFoundException:
                statusCode = HttpStatusCode.NotFound;
                error = "NotFound";
                message = exception.Message;
                break;

            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Forbidden;
                error = "Forbidden";
                message = exception.Message;
                break;

            case ArgumentException:
                statusCode = HttpStatusCode.BadRequest;
                error = "BadRequest";
                message = exception.Message;
                break;

            case InvalidOperationException:
                statusCode = HttpStatusCode.BadRequest;
                error = "BadRequest";
                message = exception.Message;
                break;
        }

        var response = new ErrorResponseDto
        {
            Error = error,
            Message = message,
            TraceId = traceId
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return context.Response.WriteAsync(json);
    }
}
