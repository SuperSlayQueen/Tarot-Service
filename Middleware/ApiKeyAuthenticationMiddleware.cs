using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using test_project.Services.Interfaces;

namespace test_project.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.StartsWith("/swagger") || path.StartsWith("/health"))
        {
            await _next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue("X-API-KEY", out var apiKeyValue))
        {
            var apiKey = apiKeyValue.ToString();
            var isValid = await authService.ValidateApiKeyAsync(apiKey);

            if (isValid)
            {
                _logger.LogInformation("Валидный API ключ используется");
                
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Role, "Admin")
                };
                
                var identity = new ClaimsIdentity(claims, "ApiKey");
                var principal = new ClaimsPrincipal(identity);
                
                context.User = principal;
                context.Items["UserRole"] = "Admin";
                
                await _next(context);
                return;
            }
            else
            {
                _logger.LogWarning("Невалидный API ключ: {ApiKey}", apiKey);
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Unauthorized\",\"message\":\"Invalid API Key\"}");
                return;
            }
        }

        await _next(context);
    }
}
