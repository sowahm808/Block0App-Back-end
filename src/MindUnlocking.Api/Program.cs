using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

using Asp.Versioning;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using MindUnlocking.Application.Options;
using MindUnlocking.Application.Security;
using MindUnlocking.Contracts.Auth;
using MindUnlocking.Infrastructure.Auth;
using MindUnlocking.Infrastructure.Identity;
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
// Register
// ------------------------------------------------------------

v1.MapPost(
    "/auth/register",
    async (
        RegisterRequest request,
        UserManager<ApplicationUser> users) =>
    {
        var existingUser =
            await users.FindByEmailAsync(request.Email);

        if (existingUser is not null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["email"] =
                    [
                        "An account with this email address already exists."
                    ]
                });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var result =
            await users.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                ToValidationDictionary(result));
        }

        await users.AddClaimAsync(
            user,
            new Claim(
                AuthorizationPolicies.PermissionClaimType,
                MindUnlocking.Domain.Identity.Permissions
                    .ScholarAccess));

        var confirmationToken =
            await users.GenerateEmailConfirmationTokenAsync(user);

        return Results.Created(
            $"/api/v1/auth/users/{user.Id}",
            new RegisterResponse(
                user.Id,
                user.Email!,
                confirmationToken));
    })
    .AllowAnonymous();

// ------------------------------------------------------------
// Verify email
// ------------------------------------------------------------

v1.MapPost(
    "/auth/verify-email",
    async (
        VerifyEmailRequest request,
        UserManager<ApplicationUser> users) =>
    {
        var user =
            await users.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return Results.NotFound();
        }

        var result =
            await users.ConfirmEmailAsync(user, request.Token);

        return result.Succeeded
            ? Results.NoContent()
            : Results.ValidationProblem(
                ToValidationDictionary(result));
    })
    .AllowAnonymous();

// ------------------------------------------------------------
// Login
// ------------------------------------------------------------

v1.MapPost(
    "/auth/login",
    async (
        LoginRequest request,
        UserManager<ApplicationUser> users,
        MindUnlockingDbContext db,
        AuthTokenService tokens,
        IOptions<JwtOptions> jwtConfiguration) =>
    {
        var user =
            await users.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var passwordValid =
            await users.CheckPasswordAsync(
                user,
                request.Password);

        if (!passwordValid)
        {
            return Results.Unauthorized();
        }

        if (!await users.IsEmailConfirmedAsync(user))
        {
            return Results.Problem(
                detail:
                    "Email verification is required before sign-in.",
                statusCode:
                    StatusCodes.Status403Forbidden);
        }

        var requiresMfa =
            user.AdministrativeMfaRequired ||
            await users.GetTwoFactorEnabledAsync(user);

        if (requiresMfa &&
            string.IsNullOrWhiteSpace(request.MfaCode))
        {
            return Results.Problem(
                detail: "MFA code is required.",
                statusCode:
                    StatusCodes.Status403Forbidden);
        }

        if (!string.IsNullOrWhiteSpace(request.MfaCode))
        {
            var validMfaCode =
                await users.VerifyTwoFactorTokenAsync(
                    user,
                    TokenOptions.DefaultAuthenticatorProvider,
                    request.MfaCode);

            if (!validMfaCode)
            {
                return Results.Unauthorized();
            }
        }

        return await IssueTokenResponse(
            user,
            users,
            db,
            tokens,
            jwtConfiguration.Value);
    })
    .AllowAnonymous();

// ------------------------------------------------------------
// Refresh token
// ------------------------------------------------------------

v1.MapPost(
    "/auth/refresh",
    async (
        RefreshTokenRequest request,
        UserManager<ApplicationUser> users,
        MindUnlockingDbContext db,
        AuthTokenService tokens,
        IOptions<JwtOptions> jwtConfiguration) =>
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash =
            tokens.HashRefreshToken(request.RefreshToken);

        var session =
            await db.RefreshSessions.SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash);

        if (session is null)
        {
            return Results.Unauthorized();
        }

        if (!session.IsActive(now))
        {
            await db.RefreshSessions
                .Where(x =>
                    x.UserId == session.UserId &&
                    x.RevokedUtc == null)
                .ExecuteUpdateAsync(update =>
                    update
                        .SetProperty(
                            item => item.RevokedUtc,
                            now)
                        .SetProperty(
                            item => item.RevocationReason,
                            "refresh-token-reuse-detected"));

            return Results.Unauthorized();
        }

        var user =
            await users.FindByIdAsync(
                session.UserId.ToString());

        if (user is null)
        {
            return Results.Unauthorized();
        }

        session.RevokedUtc = now;
        session.RevocationReason = "rotated";

        var newRefreshToken =
            tokens.CreateRefreshToken();

        session.ReplacedByTokenHash =
            tokens.HashRefreshToken(newRefreshToken);

        db.RefreshSessions.Add(
            new RefreshSession
            {
                UserId = user.Id,
                TokenHash = session.ReplacedByTokenHash,
                ExpiresUtc = now.AddDays(
                    jwtConfiguration.Value.RefreshTokenDays)
            });

        await db.SaveChangesAsync();

        var permissions =
            (await users.GetClaimsAsync(user))
            .Where(claim =>
                claim.Type ==
                AuthorizationPolicies.PermissionClaimType)
            .Select(claim => claim.Value)
            .Distinct()
            .ToArray();

        var accessToken =
            tokens.CreateAccessToken(
                user,
                permissions,
                now,
                out var accessTokenExpiresUtc);

        return Results.Ok(
            new TokenResponse(
                accessToken,
                accessTokenExpiresUtc,
                newRefreshToken,
                now.AddDays(
                    jwtConfiguration.Value.RefreshTokenDays)));
    })
    .AllowAnonymous();

// ------------------------------------------------------------
// Forgot password
// ------------------------------------------------------------

v1.MapPost(
    "/auth/forgot-password",
    async (
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> users,
        IWebHostEnvironment environment) =>
    {
        var user =
            await users.FindByEmailAsync(request.Email);

        // Do not reveal whether the email exists.
        if (user is null)
        {
            return Results.Accepted(
                value: new ForgotPasswordResponse(null));
        }

        var token =
            await users.GeneratePasswordResetTokenAsync(user);

        return Results.Accepted(
            value: new ForgotPasswordResponse(
                environment.IsDevelopment()
                    ? token
                    : null));
    })
    .AllowAnonymous();

// ------------------------------------------------------------
// Reset password
// ------------------------------------------------------------

v1.MapPost(
    "/auth/reset-password",
    async (
        ResetPasswordRequest request,
        UserManager<ApplicationUser> users) =>
    {
        var user =
            await users.FindByEmailAsync(request.Email);

        // Do not reveal whether an account exists.
        if (user is null)
        {
            return Results.NoContent();
        }

        var result =
            await users.ResetPasswordAsync(
                user,
                request.Token,
                request.NewPassword);

        return result.Succeeded
            ? Results.NoContent()
            : Results.ValidationProblem(
                ToValidationDictionary(result));
    })
    .AllowAnonymous();

// ------------------------------------------------------------
// Revoke refresh token
// ------------------------------------------------------------

v1.MapPost(
    "/auth/revoke",
    async (
        RevokeRefreshTokenRequest request,
        MindUnlockingDbContext db,
        AuthTokenService tokens) =>
    {
        var tokenHash =
            tokens.HashRefreshToken(request.RefreshToken);

        var session =
            await db.RefreshSessions.SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash);

        if (session is not null &&
            session.RevokedUtc is null)
        {
            session.RevokedUtc =
                DateTimeOffset.UtcNow;

            session.RevocationReason =
                request.Reason;

            await db.SaveChangesAsync();
        }

        return Results.NoContent();
    })
    .RequireAuthorization();

// ------------------------------------------------------------
// Logout
// ------------------------------------------------------------

v1.MapPost(
    "/auth/logout",
    async (
        HttpContext httpContext,
        MindUnlockingDbContext db) =>
    {
        var userId =
            httpContext.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (Guid.TryParse(userId, out var parsedUserId))
        {
            var now = DateTimeOffset.UtcNow;

            await db.RefreshSessions
                .Where(session =>
                    session.UserId == parsedUserId &&
                    session.RevokedUtc == null)
                .ExecuteUpdateAsync(update =>
                    update
                        .SetProperty(
                            session => session.RevokedUtc,
                            now)
                        .SetProperty(
                            session => session.RevocationReason,
                            "logout"));
        }

        return Results.NoContent();
    })
    .RequireAuthorization();

// ------------------------------------------------------------
// Current user
// ------------------------------------------------------------

v1.MapGet(
    "/auth/me",
    async (
        HttpContext httpContext,
        UserManager<ApplicationUser> users) =>
    {
        var user =
            await users.GetUserAsync(httpContext.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var permissions =
            (await users.GetClaimsAsync(user))
            .Where(claim =>
                claim.Type ==
                AuthorizationPolicies.PermissionClaimType)
            .Select(claim => claim.Value)
            .Distinct()
            .ToArray();

        return Results.Ok(
            new CurrentUserResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                permissions,
                user.EmailConfirmed,
                user.TwoFactorEnabled));
    })
    .RequireAuthorization();

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

static Dictionary<string, string[]> ToValidationDictionary(
    IdentityResult result)
{
    return result.Errors
        .GroupBy(error => error.Code)
        .ToDictionary(
            group => group.Key,
            group => group
                .Select(error => error.Description)
                .ToArray());
}

static async Task<IResult> IssueTokenResponse(
    ApplicationUser user,
    UserManager<ApplicationUser> users,
    MindUnlockingDbContext db,
    AuthTokenService tokens,
    JwtOptions jwtOptions)
{
    var now = DateTimeOffset.UtcNow;

    var permissions =
        (await users.GetClaimsAsync(user))
        .Where(claim =>
            claim.Type ==
            AuthorizationPolicies.PermissionClaimType)
        .Select(claim => claim.Value)
        .Distinct()
        .ToArray();

    var accessToken =
        tokens.CreateAccessToken(
            user,
            permissions,
            now,
            out var accessTokenExpiresUtc);

    var refreshToken =
        tokens.CreateRefreshToken();

    db.RefreshSessions.Add(
        new RefreshSession
        {
            UserId = user.Id,
            TokenHash =
                tokens.HashRefreshToken(refreshToken),
            ExpiresUtc =
                now.AddDays(jwtOptions.RefreshTokenDays)
        });

    await db.SaveChangesAsync();

    return Results.Ok(
        new TokenResponse(
            accessToken,
            accessTokenExpiresUtc,
            refreshToken,
            now.AddDays(jwtOptions.RefreshTokenDays)));
}

public partial class Program
{
}