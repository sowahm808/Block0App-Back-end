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
v1.MapGet("/auth/me", () => Results.Ok()).RequireAuthorization();
v1.MapPost("/auth/register", () => Results.Accepted()).AllowAnonymous();
v1.MapPost("/auth/login", () => Results.Accepted()).AllowAnonymous();
v1.MapPost("/auth/refresh", () => Results.Accepted()).AllowAnonymous();
v1.MapPost("/auth/logout", () => Results.NoContent()).RequireAuthorization();
v1.MapGet("/readiness/current", () => Results.Ok(new { disclaimer = MindUnlocking.Domain.Readiness.ReadinessEngine.Disclaimer })).RequireAuthorization(MindUnlocking.Domain.Identity.Permissions.ScholarAccess);
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.Run();

public partial class Program { }
