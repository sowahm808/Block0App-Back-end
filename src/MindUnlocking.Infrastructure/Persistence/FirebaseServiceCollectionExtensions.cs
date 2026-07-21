using FirebaseAdmin;
using FirebaseAdmin.Auth;
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
            var credential = CreateGoogleCredential(options);
            if (credential is not null)
            {
                appOptions.Credential = credential;
            }

            return FirebaseApp.Create(appOptions);
        });

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<FirebaseOptions>>().Value;
            var builder = new FirestoreDbBuilder { ProjectId = options.ProjectId };
            var credential = CreateGoogleCredential(options);
            if (credential is not null)
            {
                builder.Credential = credential;
            }

            return builder.Build();
        });
        services.AddSingleton(provider => FirebaseAuth.GetAuth(provider.GetRequiredService<FirebaseApp>()));
        services.AddSingleton<FirebaseUserStore>();
        services.AddSingleton<AuthTokenService>();
        services.AddScoped<IAuthUseCases, AuthUseCases>();
        return services;
    }

    private static GoogleCredential? CreateGoogleCredential(FirebaseOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ServiceAccountJson))
        {
            return GoogleCredential.FromJson(options.ServiceAccountJson);
        }

        if (!string.IsNullOrWhiteSpace(options.ServiceAccountPath))
        {
            return GoogleCredential.FromFile(options.ServiceAccountPath);
        }

        return null;
    }
}
