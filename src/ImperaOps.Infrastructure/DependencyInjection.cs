using Hangfire;
using ImperaOps.Application.Abstractions;
using ImperaOps.Infrastructure.Auth;
using ImperaOps.Infrastructure.Data;
using ImperaOps.Infrastructure.Email;
using ImperaOps.Infrastructure.Jobs;
using ImperaOps.Infrastructure.Notifications;
using ImperaOps.Infrastructure.Repositories;
using ImperaOps.Infrastructure.Services;
using ImperaOps.Infrastructure.Storage;
using ImperaOps.Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Resend;

namespace ImperaOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Database") ?? throw new InvalidOperationException("Missing ConnectionStrings:Database");

        services.AddDbContext<ImperaOpsDbContext>(options =>
        {
            options.UseMySql(cs, ServerVersion.AutoDetect(cs));
        });

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventReadRepository>(_ => new EventReadRepository(cs));
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddSingleton<IStorageService, S3StorageService>();
        services.AddSingleton<ICounterService>(_ => new CounterService(cs));

        // Email — Resend
        services.AddOptions<ResendClientOptions>().Configure(opts =>
        {
            opts.ApiToken = config["Email:ResendApiKey"] ?? throw new InvalidOperationException("Missing Email:ResendApiKey");
        });
        services.AddHttpClient<ResendClient>();
        services.AddTransient<IResend, ResendClient>();
        services.AddScoped<IEmailService, ResendEmailService>();
        services.AddSingleton<INotificationPushService, NotificationPushService>();
        services.AddScoped<INotificationService, NotificationService>();

        // Hangfire job classes (resolved by the job server via DI)
        services.AddScoped<TaskReminderJob>();
        services.AddScoped<SlaEscalationJob>();
        services.AddScoped<WebhookDeliveryJob>();

        // Webhooks
        services.AddHttpClient("WebhookClient").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();

        return services;
    }
}
