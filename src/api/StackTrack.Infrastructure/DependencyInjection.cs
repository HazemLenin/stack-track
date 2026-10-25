using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackTrack.Infrastructure.Persistence;

namespace StackTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}
