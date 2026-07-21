using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MindUnlocking.Application.Options;
using MindUnlocking.Application.Security;
using MindUnlocking.Infrastructure.Auth;

namespace MindUnlocking.Infrastructure.Persistence;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMindUnlockingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FirebaseOptions>()
            .Bind(configuration.GetSection("Firebase"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;
            if (FirebaseApp.DefaultInstance is not null)
            {
                return FirebaseApp.DefaultInstance;
            }

            var appOptions = new AppOptions { ProjectId = options.ProjectId };
            if (!string.IsNullOrWhiteSpace(options.ServiceAccountPath))
            {
                appOptions.Credential = GoogleCredential.FromFile(options.ServiceAccountPath);
            }

            return FirebaseApp.Create(appOptions);
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;
            var builder = new FirestoreDbBuilder { ProjectId = options.ProjectId };
            if (!string.IsNullOrWhiteSpace(options.ServiceAccountPath))
            {
                builder.Credential = GoogleCredential.FromFile(options.ServiceAccountPath);
            }

            return builder.Build();
        });
        services.AddSingleton<FirebaseUserStore>();
        services.AddSingleton<AuthTokenService>();
        services.AddScoped<IAuthUseCases, AuthUseCases>();
        return services;
    }
}
