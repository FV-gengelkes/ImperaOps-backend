using FreightVis.Application.Abstractions;
using FreightVis.Infrastructure.Auth;
using FreightVis.Infrastructure.Data;
using FreightVis.Infrastructure.Repositories;
using FreightVis.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreightVis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Database") ?? throw new InvalidOperationException("Missing ConnectionStrings:Database");

        services.AddDbContext<FreightVisDbContext>(options =>
        {
            options.UseMySql(cs, ServerVersion.AutoDetect(cs));
        });

        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IIncidentReadRepository>(_ => new IncidentReadRepository(cs));
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IStorageService, S3StorageService>();

        return services;
    }
}
