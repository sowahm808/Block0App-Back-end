using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MindUnlocking.Application.Options;
using MindUnlocking.Infrastructure.Identity;

namespace MindUnlocking.Infrastructure.Persistence;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMindUnlockingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var sql = configuration.GetSection("Sql").Get<SqlOptions>() ?? new SqlOptions();
        services.AddDbContext<MindUnlockingDbContext>(options => options.UseSqlServer(sql.ConnectionString, sqlServer => sqlServer.EnableRetryOnFailure(sql.MaxRetryCount)));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedEmail = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireNonAlphanumeric = true;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<MindUnlockingDbContext>()
        .AddDefaultTokenProviders();
        return services;
    }
}
