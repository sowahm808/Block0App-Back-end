using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

using Asp.Versioning;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

using MindUnlocking.Application.Options;
using MindUnlocking.Application.Security;
using MindUnlocking.Contracts.Auth;
using MindUnlocking.Infrastructure.Persistence;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// Logging
// ------------------------------------------------------------

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();
});

// ------------------------------------------------------------
// Strongly typed configuration
// ------------------------------------------------------------

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<SqlOptions>()
    .Bind(builder.Configuration.GetSection("Sql"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<ChallengeOptions>()
    .Bind(builder.Configuration.GetSection("Challenge"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ------------------------------------------------------------
// Error handling
// ------------------------------------------------------------

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});

// ------------------------------------------------------------
// API versioning
// ------------------------------------------------------------

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// ------------------------------------------------------------
// Swagger
// ------------------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ------------------------------------------------------------
// Infrastructure
// ------------------------------------------------------------

builder.Services.AddMindUnlockingInfrastructure(builder.Configuration);

// ------------------------------------------------------------
// JWT authentication
// ------------------------------------------------------------

var jwtOptions =
    builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "The Jwt configuration section is missing.");

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be configured.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,

            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),

            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// ------------------------------------------------------------
// Authorization
// ------------------------------------------------------------

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in AuthorizationPolicies.All)
    {
        options.AddPolicy(
            permission,
            policy => policy.RequireClaim(
                AuthorizationPolicies.PermissionClaimType,
                permission));
    }
});

// ------------------------------------------------------------
// CORS
// ------------------------------------------------------------

var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfiguredOrigins", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ------------------------------------------------------------
// Rate limiting
// ------------------------------------------------------------
//
// This uses AddPolicy instead of AddFixedWindowLimiter.
// It avoids the extension-method compilation issue while still
// applying a fixed-window rate limiter.
//

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy<string>("api", httpContext =>
    {
        var partitionKey =
            httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? "authenticated"
                : httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder =
                    QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
});

// ------------------------------------------------------------
// Health checks
// ------------------------------------------------------------

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<MindUnlockingDbContext>(
        name: "sql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var app = builder.Build();

// ------------------------------------------------------------
// Middleware pipeline
// ------------------------------------------------------------

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd(
        "X-Content-Type-Options",
        "nosniff");

    context.Response.Headers.TryAdd(
        "X-Frame-Options",
        "DENY");

    context.Response.Headers.TryAdd(
        "Referrer-Policy",
        "no-referrer");

    context.Response.Headers.TryAdd(
        "Content-Security-Policy",
        "default-src 'none'; frame-ancestors 'none'");

    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("AllowConfiguredOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

// ------------------------------------------------------------
// API v1
// ------------------------------------------------------------

var v1 = app
    .MapGroup("/api/v1")
    .WithApiVersionSet(
        app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build())
    .MapToApiVersion(new ApiVersion(1, 0))
    .RequireRateLimiting("api");

// ------------------------------------------------------------
// API aliases
// ------------------------------------------------------------

// Keep the versioned route as the canonical API while also exposing
// /api/* for existing clients configured with a /api base URL.
MapAuthEndpoints(v1, "/api/v1");

var unversionedApi = app
    .MapGroup("/api")
    .RequireRateLimiting("api");

MapAuthEndpoints(unversionedApi, "/api");

// ------------------------------------------------------------
// Readiness
// ------------------------------------------------------------

v1.MapGet(
    "/readiness/current",
    () => Results.Ok(
        new
        {
            disclaimer =
                MindUnlocking.Domain.Readiness
                    .ReadinessEngine.Disclaimer
        }))
    .RequireAuthorization(
        MindUnlocking.Domain.Identity.Permissions
            .ScholarAccess);

// ------------------------------------------------------------
// Health endpoints
// ------------------------------------------------------------

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    });

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check =>
            check.Tags.Contains("ready")
    });

app.Run();

// ------------------------------------------------------------
// Helper methods
// ------------------------------------------------------------

static void MapAuthEndpoints(RouteGroupBuilder group, string routePrefix)
{
    group.MapPost(
        "/auth/register",
        async (RegisterRequest request, IAuthUseCases auth, CancellationToken cancellationToken) =>
        {
            var result = await auth.RegisterAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Created($"{routePrefix}/auth/users/{result.Value!.UserId}", result.Value)
                : ToHttpResult(result);
        })
        .AllowAnonymous();

    group.MapPost(
        "/auth/verify-email",
        async (VerifyEmailRequest request, IAuthUseCases auth, CancellationToken cancellationToken) =>
        {
            var result = await auth.VerifyEmailAsync(request, cancellationToken);
            return result.Succeeded ? Results.NoContent() : ToHttpResult(result);
        })
        .AllowAnonymous();

    group.MapPost(
        "/auth/login",
        async (LoginRequest request, IAuthUseCases auth, CancellationToken cancellationToken) =>
        {
            var result = await auth.LoginAsync(request, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : ToHttpResult(result);
        })
        .AllowAnonymous();

    group.MapPost(
        "/auth/refresh",
        async (RefreshTokenRequest request, IAuthUseCases auth, CancellationToken cancellationToken) =>
        {
            var result = await auth.RefreshAsync(request, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : ToHttpResult(result);
        })
        .AllowAnonymous();

    group.MapPost(
        "/auth/forgot-password",
        async (ForgotPasswordRequest request, IAuthUseCases auth, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
            Results.Accepted(value: await auth.ForgotPasswordAsync(request, environment.IsDevelopment(), cancellationToken)))
        .AllowAnonymous();

    group.MapPost(
        "/auth/reset-password",
        async (ResetPasswordRequest request, IAuthUseCases auth, CancellationToken cancellationToken) =>
        {
            var result = await auth.ResetPasswordAsync(request, cancellationToken);
            return result.Succeeded ? Results.NoContent() : ToHttpResult(result);
        })
        .AllowAnonymous();

    group.MapPost(
        "/auth/revoke",
        async (RevokeRefreshTokenRequest request, IAuthUseCases auth, CancellationToken cancellationToken) =>
        {
            await auth.RevokeRefreshTokenAsync(request, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization();

    group.MapPost(
        "/auth/logout",
        async (HttpContext httpContext, IAuthUseCases auth, CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userId, out var parsedUserId))
            {
                await auth.LogoutAsync(parsedUserId, cancellationToken);
            }

            return Results.NoContent();
        })
        .RequireAuthorization();

    group.MapGet(
        "/auth/me",
        async (HttpContext httpContext, IAuthUseCases auth, CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                return Results.Unauthorized();
            }

            var result = await auth.GetCurrentUserAsync(parsedUserId, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : ToHttpResult(result);
        })
        .RequireAuthorization();
}

static IResult ToHttpResult<T>(AuthUseCaseResult<T> result) =>
    result.ErrorCode switch
    {
        AuthErrorCode.DuplicateEmail or AuthErrorCode.ValidationFailed =>
            Results.ValidationProblem(result.ValidationErrors?.ToDictionary(x => x.Key, x => x.Value) ?? []),
        AuthErrorCode.EmailVerificationRequired =>
            Results.Problem(detail: "Email verification is required before sign-in.", statusCode: StatusCodes.Status403Forbidden),
        AuthErrorCode.MfaRequired =>
            Results.Problem(detail: "MFA code is required.", statusCode: StatusCodes.Status403Forbidden),
        AuthErrorCode.UserNotFound => Results.NotFound(),
        AuthErrorCode.InvalidCredentials or AuthErrorCode.InvalidMfaCode or AuthErrorCode.InvalidRefreshToken =>
            Results.Unauthorized(),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };

public partial class Program
{
}
