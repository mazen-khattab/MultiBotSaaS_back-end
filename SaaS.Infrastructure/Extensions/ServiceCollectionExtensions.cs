using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SaaS.Application.Common.Interfaces;
using SaaS.Application.Common.Services;
using SaaS.Infrastructure.Persistence;
using SaaS.Infrastructure.Services;
using SaaS.Infrastructure.Strategies;


namespace SaaS.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<AppDbContext>(options => 
                options.UseSqlServer(connectionString,
                    sqlOptions => sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null)));

            // 2. Bind the Application Interface to the Infrastructure Implementation
            services.AddScoped<IAppDbContext>(provider =>
                provider.GetRequiredService<AppDbContext>());

            services.AddSingleton<IEncryptionService, EncryptionService>();

            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<ISessionTokenValidator, SessionTokenValidator>();
            services.AddScoped<IPasswordHasherService, PasswordHasherService>();
            services.AddScoped<IAuthSessionService, AuthSessionService>();
            services.AddScoped<IUserBotService, UserBotService>();
            services.AddScoped<IAdminUserPersistenceService, AdminUserPersistenceService>();
            services.AddScoped<IAdminBotPersistenceService, AdminBotPersistenceService>();
            services.AddScoped<IAdminSubscriptionPersistenceService, AdminSubscriptionPersistenceService>();

            services.AddScoped<IExternalSystemRequestStrategy, N8nRequestStrategy>();
            services.AddScoped<IExternalSystemRequestStrategy, NodeWorkerRequestStrategy>();
            services.AddScoped<INetworkClient, NetworkClient>();

            services.AddHttpClient(NetworkClient.NamedClient, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddResilienceHandler("external-systems-pipeline", builder =>
            {
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    ShouldHandle = args => ValueTask.FromResult(
                        HttpClientResiliencePredicates.IsTransient(args.Outcome))
                });

                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 1.0, 
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(30),
                    ShouldHandle = args => ValueTask.FromResult(
                        HttpClientResiliencePredicates.IsTransient(args.Outcome))
                });
            });

            // SingalR services
            services.AddSignalR();
            services.AddTransient<IAppNotificationService, SignalRNotificationService>();

            services.AddSingleton<IN8nWebhookResolver, N8nWebhookResolver>();

            // Register background hosted services
            services.AddHostedService<JobTimeoutWatchdogService>();
            services.AddHostedService<AccountCooldownWatchdogService>();

            services.AddSingleton<IJobStalenessStrategy, MessagingJobStalenessStrategy>();

            return services;
        }
    }
}
