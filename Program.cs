using System.Security.Claims;
using System.Text;
using FluentValidation;
using HealthChecks.NpgSql;
using HealthChecks.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using test_project.Data;
using test_project.Middleware;
using test_project.Repositories;
using test_project.Repositories.Interfaces;
using test_project.Services;
using test_project.Services.Interfaces;
using test_project.Validators;

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--shard-lab-setup"))
{
    await test_project.Sharding.ShardLabSetup.RunAsync(builder.Configuration);
    return;
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tarot Reading Service API",
        Version = "v1",
        Description = "API для сервиса раскладов Таро"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var replicaConnectionString = builder.Configuration.GetConnectionString("ReplicaConnection")
    ?? connectionString;
builder.Services.AddDbContext<TarotDbContext>(options =>
    options.UseNpgsql(connectionString));
// Lab 4: отдельный DbContext на PostgreSQL Replica для read-запросов
builder.Services.AddDbContext<TarotReadDbContext>(options =>
    options.UseNpgsql(replicaConnectionString));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
});

var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "your-super-secret-key-change-this-in-production-minimum-32-characters-long";
var key = Encoding.UTF8.GetBytes(jwtSecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5),
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role,
        RequireExpirationTime = true,
        RequireSignedTokens = true
    };
    
    options.SaveToken = true;
    
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var authHeader = context.Request.Headers["Authorization"].ToString();
            var hasAuthHeader = !string.IsNullOrEmpty(authHeader);
            
            if (hasAuthHeader && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                context.Token = token;
                logger.LogInformation("JWT OnMessageReceived. Path: {Path}, Token extracted. Token length: {TokenLength}", 
                    context.Request.Path, token?.Length ?? 0);
            }
            else if (hasAuthHeader)
            {
                logger.LogWarning("Authorization header does not start with 'Bearer ' for path: {Path}, Header: {Header}", 
                    context.Request.Path, authHeader.Substring(0, Math.Min(30, authHeader.Length)));
            }
            else
            {
                logger.LogWarning("Authorization header is missing for path: {Path}", context.Request.Path);
            }
            
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();
            logger.LogError(context.Exception, "JWT Authentication failed. Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}. AuthHeader: {AuthHeader}", 
                context.Exception.GetType().Name, 
                context.Exception.Message, 
                context.Exception.StackTrace, 
                string.IsNullOrEmpty(authHeader) ? "MISSING" : authHeader.Substring(0, Math.Min(50, authHeader.Length)) + "...");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
            logger.LogInformation("JWT Token validated successfully. UserId: {UserId}, Role: {Role}, IsAuthenticated: {IsAuthenticated}", 
                userId, role, context.Principal?.Identity?.IsAuthenticated);
            return Task.CompletedTask;
        },
        OnChallenge = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            
            var isAuthenticated = context.HttpContext.User?.Identity?.IsAuthenticated ?? false;
            var userId = context.HttpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = context.HttpContext.User?.FindFirst(ClaimTypes.Role)?.Value;
            
            logger.LogWarning("OnChallenge triggered. IsAuthenticated: {IsAuthenticated}, UserId: {UserId}, Role: {Role}, Error: {Error}, ErrorDescription: {ErrorDescription}", 
                isAuthenticated, userId, role, context.Error, context.ErrorDescription);
            
            if (isAuthenticated)
            {
                logger.LogWarning("Authenticated user (Role: {Role}) lacks required role. Returning 403 Forbidden.", role);
                context.HandleResponse();
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                var response403 = new { error = "Forbidden", message = "Доступ запрещен. Недостаточно прав." };
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response403));
                return;
            }
            
            logger.LogWarning("User not authenticated. Returning 401 Unauthorized.");
            context.HandleResponse();
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var response401 = new { error = "Unauthorized", message = "Требуется аутентификация. Проверьте, что токен валиден и не истек." };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response401));
        },
        OnForbidden = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var userId = context.HttpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = context.HttpContext.User?.FindFirst(ClaimTypes.Role)?.Value;
            logger.LogWarning("OnForbidden triggered. UserId: {UserId}, Role: {Role}", userId, role);
            
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            var response = new { error = "Forbidden", message = "Доступ запрещен. Недостаточно прав." };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = null;
    
    options.AddPolicy("AdminOrManager", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin", "Manager");
    });
    
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
    });
    
    options.InvokeHandlersAfterFailure = false;
    options.DefaultPolicy = null;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddScoped<ICardRepository, CardRepository>();
builder.Services.AddScoped<IDeckRepository, DeckRepository>();
builder.Services.AddScoped<ISpreadRepository, SpreadRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
var shardOptions = builder.Configuration.GetSection("Sharding").Get<test_project.Sharding.ShardOptions>()
    ?? new test_project.Sharding.ShardOptions();
if (shardOptions.Enabled)
{
    builder.Services.AddSingleton(new test_project.Sharding.ShardConnections(shardOptions));
    builder.Services.AddScoped<IReadingRepository, ShardedReadingRepository>();
    builder.Services.AddScoped<IStatsRepository, ShardedStatsRepository>();
}
else
{
    // Labs 1–4: preserve the original primary/replica repositories.
    builder.Services.AddScoped<IReadingRepository, ReadingRepository>();
    builder.Services.AddScoped<IStatsRepository, StatsRepository>();
}
builder.Services.AddScoped<IApiKeyRepository>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
    return new ApiKeyRepository(connectionString!);
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddScoped<IDeckService, DeckService>();
builder.Services.AddScoped<ISpreadService, SpreadService>();
builder.Services.AddScoped<IReadingService, ReadingService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddSingleton<IAlertService, AlertService>();
builder.Services.AddSingleton<IPartitionService, PartitionService>();
builder.Services.AddHostedService<CreatePartitionsJob>();
builder.Services.AddHostedService<PartitionHealthCheckService>();

builder.Services.AddValidatorsFromAssemblyContaining<CreateCardDtoValidator>();

var healthChecks = builder.Services.AddHealthChecks();
healthChecks.AddNpgSql(connectionString!, name: "postgresql");
healthChecks.AddRedis(redisConnectionString!, name: "redis");
healthChecks.AddCheck("self", () => HealthCheckResult.Healthy("API is healthy"), tags: new[] { "api" });

var app = builder.Build();
if (shardOptions.Enabled)
    await app.Services.GetRequiredService<test_project.Sharding.ShardConnections>().VerifyTopologyAsync();

app.UseRequestLoggingMiddleware();
app.UseErrorHandlingMiddleware();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();


if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tarot Reading Service API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLoggingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }

    public static IApplicationBuilder UseErrorHandlingMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }
}
