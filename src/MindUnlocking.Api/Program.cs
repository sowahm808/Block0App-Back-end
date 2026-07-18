using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MindUnlocking.Contracts.Auth;
using MindUnlocking.Infrastructure.Auth;
using MindUnlocking.Infrastructure.Identity;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MindUnlocking.Application.Options;
using MindUnlocking.Application.Security;
using MindUnlocking.Infrastructure.Persistence;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext());
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection("Jwt")).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SqlOptions>().Bind(builder.Configuration.GetSection("Sql")).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ChallengeOptions>().Bind(builder.Configuration.GetSection("Challenge")).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx => ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddApiVersioning(options => options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0)).AddApiExplorer();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMindUnlockingInfrastructure(builder.Configuration);
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
    };
});
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in AuthorizationPolicies.All)
    {
        options.AddPolicy(permission, policy => policy.RequireClaim(AuthorizationPolicies.PermissionClaimType, permission));
    }
});
builder.Services.AddCors(options => options.AddPolicy("AllowConfiguredOrigins", policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("api", limiter => { limiter.PermitLimit = 120; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; limiter.QueueLimit = 0; }));
builder.Services.AddHealthChecks().AddDbContextCheck<MindUnlockingDbContext>("sql");

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
    await next();
});
if (!app.Environment.IsDevelopment()) { app.UseHsts(); }
app.UseHttpsRedirection();
app.UseCors("AllowConfiguredOrigins");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();

var v1 = app.MapGroup("/api/v1").RequireRateLimiting("api");

v1.MapPost("/auth/register", async (RegisterRequest request, UserManager<ApplicationUser> users) =>
{
    var user = new ApplicationUser { UserName = request.Email, Email = request.Email, DisplayName = request.DisplayName };
    var result = await users.CreateAsync(user, request.Password);
    if (!result.Succeeded) return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
    await users.AddClaimAsync(user, new Claim(AuthorizationPolicies.PermissionClaimType, MindUnlocking.Domain.Identity.Permissions.ScholarAccess));
    var token = await users.GenerateEmailConfirmationTokenAsync(user);
    return Results.Created($"/api/v1/auth/users/{user.Id}", new RegisterResponse(user.Id, user.Email!, token));
}).AllowAnonymous();

v1.MapPost("/auth/verify-email", async (VerifyEmailRequest request, UserManager<ApplicationUser> users) =>
{
    var user = await users.FindByEmailAsync(request.Email);
    if (user is null) return Results.NotFound();
    var result = await users.ConfirmEmailAsync(user, request.Token);
    return result.Succeeded ? Results.NoContent() : Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
}).AllowAnonymous();

v1.MapPost("/auth/login", async (LoginRequest request, UserManager<ApplicationUser> users, MindUnlockingDbContext db, AuthTokenService tokens, IOptions<JwtOptions> jwtOptions) =>
{
    var user = await users.FindByEmailAsync(request.Email);
    if (user is null || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
    if (!await users.IsEmailConfirmedAsync(user)) return Results.Problem("Email verification is required before sign-in.", statusCode: StatusCodes.Status403Forbidden);
    if ((user.AdministrativeMfaRequired || await users.GetTwoFactorEnabledAsync(user)) && string.IsNullOrWhiteSpace(request.MfaCode)) return Results.Problem("MFA code is required.", statusCode: StatusCodes.Status403Forbidden);
    if (!string.IsNullOrWhiteSpace(request.MfaCode) && !await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, request.MfaCode)) return Results.Unauthorized();
    return await IssueTokenResponse(user, users, db, tokens, jwtOptions.Value);
}).AllowAnonymous();

v1.MapPost("/auth/refresh", async (RefreshTokenRequest request, UserManager<ApplicationUser> users, MindUnlockingDbContext db, AuthTokenService tokens, IOptions<JwtOptions> jwtOptions) =>
{
    var now = DateTimeOffset.UtcNow;
    var hash = tokens.HashRefreshToken(request.RefreshToken);
    var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == hash);
    if (session is null) return Results.Unauthorized();
    if (!session.IsActive(now))
    {
        await db.RefreshSessions.Where(x => x.UserId == session.UserId && x.RevokedUtc == null).ExecuteUpdateAsync(x => x.SetProperty(s => s.RevokedUtc, now).SetProperty(s => s.RevocationReason, "refresh-token-reuse-detected"));
        return Results.Unauthorized();
    }
    var user = await users.FindByIdAsync(session.UserId.ToString());
    if (user is null) return Results.Unauthorized();
    session.RevokedUtc = now;
    session.RevocationReason = "rotated";
    var refreshToken = tokens.CreateRefreshToken();
    session.ReplacedByTokenHash = tokens.HashRefreshToken(refreshToken);
    db.RefreshSessions.Add(new RefreshSession { UserId = user.Id, TokenHash = session.ReplacedByTokenHash, ExpiresUtc = now.AddDays(jwtOptions.Value.RefreshTokenDays) });
    await db.SaveChangesAsync();
    var permissions = (await users.GetClaimsAsync(user)).Where(x => x.Type == AuthorizationPolicies.PermissionClaimType).Select(x => x.Value).Distinct().ToArray();
    var accessToken = tokens.CreateAccessToken(user, permissions, now, out var accessExpiresUtc);
    return Results.Ok(new TokenResponse(accessToken, accessExpiresUtc, refreshToken, now.AddDays(jwtOptions.Value.RefreshTokenDays)));
}).AllowAnonymous();

v1.MapPost("/auth/forgot-password", async (ForgotPasswordRequest request, UserManager<ApplicationUser> users, IWebHostEnvironment env) =>
{
    var user = await users.FindByEmailAsync(request.Email);
    if (user is null) return Results.Accepted(value: new ForgotPasswordResponse(null));
    var token = await users.GeneratePasswordResetTokenAsync(user);
    return Results.Accepted(value: new ForgotPasswordResponse(env.IsDevelopment() ? token : null));
}).AllowAnonymous();

v1.MapPost("/auth/reset-password", async (ResetPasswordRequest request, UserManager<ApplicationUser> users) =>
{
    var user = await users.FindByEmailAsync(request.Email);
    if (user is null) return Results.NoContent();
    var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
    return result.Succeeded ? Results.NoContent() : Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
}).AllowAnonymous();

v1.MapPost("/auth/revoke", async (RevokeRefreshTokenRequest request, MindUnlockingDbContext db, AuthTokenService tokens) =>
{
    var hash = tokens.HashRefreshToken(request.RefreshToken);
    var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.TokenHash == hash);
    if (session is not null && session.RevokedUtc is null)
    {
        session.RevokedUtc = DateTimeOffset.UtcNow;
        session.RevocationReason = request.Reason;
        await db.SaveChangesAsync();
    }
    return Results.NoContent();
}).RequireAuthorization();

v1.MapPost("/auth/logout", async (HttpContext http, MindUnlockingDbContext db) =>
{
    var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (Guid.TryParse(userId, out var id))
    {
        var now = DateTimeOffset.UtcNow;
        await db.RefreshSessions.Where(x => x.UserId == id && x.RevokedUtc == null).ExecuteUpdateAsync(x => x.SetProperty(s => s.RevokedUtc, now).SetProperty(s => s.RevocationReason, "logout"));
    }
    return Results.NoContent();
}).RequireAuthorization();

v1.MapGet("/auth/me", async (HttpContext http, UserManager<ApplicationUser> users) =>
{
    var user = await users.GetUserAsync(http.User);
    if (user is null) return Results.Unauthorized();
    var permissions = (await users.GetClaimsAsync(user)).Where(x => x.Type == AuthorizationPolicies.PermissionClaimType).Select(x => x.Value).Distinct().ToArray();
    return Results.Ok(new CurrentUserResponse(user.Id, user.Email ?? string.Empty, user.DisplayName, permissions, user.EmailConfirmed, user.TwoFactorEnabled));
}).RequireAuthorization();

v1.MapGet("/readiness/current", () => Results.Ok(new { disclaimer = MindUnlocking.Domain.Readiness.ReadinessEngine.Disclaimer })).RequireAuthorization(MindUnlocking.Domain.Identity.Permissions.ScholarAccess);

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.Run();

static async Task<IResult> IssueTokenResponse(ApplicationUser user, UserManager<ApplicationUser> users, MindUnlockingDbContext db, AuthTokenService tokens, JwtOptions jwtOptions)
{
    var now = DateTimeOffset.UtcNow;
    var permissions = (await users.GetClaimsAsync(user)).Where(x => x.Type == AuthorizationPolicies.PermissionClaimType).Select(x => x.Value).Distinct().ToArray();
    var accessToken = tokens.CreateAccessToken(user, permissions, now, out var accessExpiresUtc);
    var refreshToken = tokens.CreateRefreshToken();
    db.RefreshSessions.Add(new RefreshSession { UserId = user.Id, TokenHash = tokens.HashRefreshToken(refreshToken), ExpiresUtc = now.AddDays(jwtOptions.RefreshTokenDays) });
    await db.SaveChangesAsync();
    return Results.Ok(new TokenResponse(accessToken, accessExpiresUtc, refreshToken, now.AddDays(jwtOptions.RefreshTokenDays)));
}
public partial class Program { }
